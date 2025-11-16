using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Fast_Mapper.Core.Abstractions;
using Fast_Mapper.Core.Exceptions;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace Fast_Mapper.Core.BaseLogic;

/// <summary>
/// Base mapper implementation (optimized)
/// </summary>
public class Mapper : IMapper
{
    private readonly MapperConfig _config;
    private const string Instance = "instance";
    private const string Value = "value";
    
    // simple thread safe caches
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> WritablePropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadablePropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, Func<object>> ConstructorCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object?>> GetterCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object?>> SetterCache = new();

    // Collection mapping caches
    private static readonly ConcurrentDictionary<Type, Type?> ElementTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Func<int, object>> ListConstructorCache = new();

    // compiled mapping delegates: Func<object source, object destination>
    // Use Lazy to avoid duplicate compilation under concurrency
    private readonly ConcurrentDictionary<(Type src, Type dst), Lazy<Func<object, object>>> _mapDelegateCache = new();

    // Compiled collection mapping delegates: Func<IEnumerable source, List<TDest>>
    private readonly ConcurrentDictionary<(Type srcElem, Type dstElem), Lazy<Func<IEnumerable, object>>> _collectionMapDelegateCache = new();

    // Cache MethodInfo lookups for type map implementations
    private static readonly ConcurrentDictionary<Type, MethodInfo?> TryGetConverterMethodCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo?> TryGetMemberMapMethodCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Mapper"/> class with the specified configuration.
    /// </summary>
    /// <param name="config">Mapper configuration used to resolve type maps and converters.</param>
    /// <exception cref="MapperArgumentNullException">Thrown when config is null.</exception>
    public Mapper(MapperConfig config)
    {
        _config = config ?? throw new MapperArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Maps a source instance to a new destination instance of type <typeparamref name="TDestination"/>.
    /// Returns default value for <typeparamref name="TDestination"/> when <paramref name="source"/> is null.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    /// <param name="source">Source instance to map.</param>
    /// <returns>Mapped destination instance.</returns>
    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null) return default!;
        return (TDestination)Map(source, typeof(TSource), typeof(TDestination));
    }

    /// <summary>
    /// Maps an enumerable of source items into a list of destination items.
    /// Returns an empty list when <paramref name="sources"/> is null.
    /// </summary>
    /// <typeparam name="TSource">Source element type.</typeparam>
    /// <typeparam name="TDestination">Destination element type.</typeparam>
    /// <param name="sources">Sequence of source elements to map.</param>
    /// <returns>List of mapped destination elements.</returns>
    public List<TDestination> Map<TSource, TDestination>(IEnumerable<TSource>? sources)
    {
        var list = new List<TDestination>();
        if (sources == null) return list;
        foreach (var s in sources)
            list.Add(Map<TSource, TDestination>(s));
        return list;
    }

    /// <summary>
    /// Maps an object of <paramref name="sourceType"/> to an instance of <paramref name="destinationType"/>.
    /// Uses configured type maps, converters, collection handling or convention-based mapping.
    /// </summary>
    /// <param name="source">Source object to map.</param>
    /// <param name="sourceType">Runtime type of the source object.</param>
    /// <param name="destinationType">Requested destination type.</param>
    /// <returns>Mapped object of <paramref name="destinationType"/> or default for value types.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when sourceType or destinationType is null.</exception>
    public object Map(object source, Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new MapperArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new MapperArgumentNullException(nameof(destinationType));

        if (source is null) return GetDefaultFor(destinationType)!;

        // If we have a TypeMap and a converter, use it.
        if (_config.TryGetMap(sourceType, destinationType, out var typeMapObj) && typeMapObj != null)
        {
            var mapType = typeMapObj.GetType();

            var tryGetConverterMethod = TryGetConverterMethodCache.GetOrAdd(mapType, t =>
                t.GetMethod("TryGetConverter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

            if (tryGetConverterMethod != null)
            {
                var args = new object?[] { null };
                var invokeResult = tryGetConverterMethod.Invoke(typeMapObj, args);
                if (invokeResult != null)
                {
                    var hasConverter = (bool)invokeResult;
                    var converterObj = args[0];
                    if (hasConverter && converterObj != null)
                    {
                        var converterDelegate = (Delegate)converterObj;
                        var result = converterDelegate.DynamicInvoke(source);
                        return result ?? GetDefaultFor(destinationType)!;
                    }
                }
            }

            if (TryHandleCollection(source, sourceType, destinationType, out var collResult))
                return collResult ?? GetDefaultFor(destinationType)!;

            return MapUsingTypeMap(source, sourceType, destinationType, typeMapObj);
        }

        if (TryHandleCollection(source, sourceType, destinationType, out var collectionResult))
            return collectionResult ?? GetDefaultFor(destinationType)!;

        // Convention-based mapping: use or build cached delegate
        var key = (src: sourceType, dst: destinationType);
        var lazy = _mapDelegateCache.GetOrAdd(key, k => new Lazy<Func<object, object>>(() => BuildMappingDelegate(k.src, k.dst), LazyThreadSafetyMode.ExecutionAndPublication));
        var mapDelegate = lazy.Value;
        return mapDelegate(source);
    }

    /// <summary>
    /// Builds and returns a compiled mapping delegate that maps an object of <paramref name="sourceType"/>
    /// to a new instance of <paramref name="destinationType"/> using cached constructor, getters and setters.
    /// </summary>
    /// <param name="sourceType">Source runtime type.</param>
    /// <param name="destinationType">Destination runtime type.</param>
    /// <returns>A function that accepts a source object and returns the mapped destination object.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when sourceType or destinationType is null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when constructor cannot be created.</exception>
    private Func<object, object> BuildMappingDelegate(Type sourceType, Type destinationType)
    {
        if (sourceType == null) throw new MapperArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new MapperArgumentNullException(nameof(destinationType));

        // prepare ctor
        var ctor = ConstructorCache.GetOrAdd(destinationType, CreateConstructor);
        if (ctor == null) throw new MapperConfigurationException($"Failed to create constructor for type '{destinationType.FullName}'");

        // collect readable and writable props
        var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p != null && p.CanWrite).ToArray());

        var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p != null && p.CanRead).ToArray());

        // build fast lookup for readable props by name (case-insensitive)
        var readableDict = readable.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // prepare getter/setter delegates for matching props
        var propPairs = new List<(Func<object, object?> getter, Action<object, object?> setter, Type srcPropType, Type dstPropType)>();

        foreach (var dstProp in writable)
        {
            if (dstProp == null) continue;

            if (dstProp.Name == null) continue;

            if (!readableDict.TryGetValue(dstProp.Name, out var srcProp)) continue;

            var getter = GetterCache.GetOrAdd(srcProp, CreateGetter);
            var setter = SetterCache.GetOrAdd(dstProp, CreateSetter);

            if (getter != null && setter != null)
            {
                propPairs.Add((getter, setter, srcProp.PropertyType, dstProp.PropertyType));
            }
        }

        // return a closure that uses cached delegates and recursive Map when nested mapping needed
        return sourceObj =>
        {
            if (sourceObj == null) throw new MapperArgumentNullException(nameof(sourceObj));

            var dest = ctor();
            if (dest == null) throw new MapperConfigurationException($"Constructor for type '{destinationType.FullName}' returned null");

            foreach (var (getter, setter, srcPropType, dstPropType) in propPairs)
            {
                try
                {
                    var val = getter(sourceObj);
                    if (val != null && _config.TryGetMap(srcPropType, dstPropType, out var nestedMap) && nestedMap != null)
                    {
                        var mapped = Map(val, srcPropType, dstPropType);
                        ApplySet(setter, dest, dstPropType, mapped);
                    }
                    else
                    {
                        ApplySet(setter, dest, dstPropType, val);
                    }
                }
                catch (MapperException)
                {
                    // Re-throw mapper exceptions as-is
                    throw;
                }
                catch (Exception ex)
                {
                    // Wrap other exceptions with context information
                    throw new MapperMappingException(
                        $"Error mapping property from type '{sourceType.FullName}' to '{destinationType.FullName}'",
                        sourceType, destinationType, srcPropType.Name, ex);
                }
            }
            return dest;
        };
    }

    /// <summary>
    /// Maps using a configured <paramref name="map"/> (ITypeMap). Honors member-level configuration such as
    /// custom resolvers and ignored members. Uses compiled accessors for performance.
    /// </summary>
    /// <param name="source">Source object to map from.</param>
    /// <param name="sourceType">Source runtime type.</param>
    /// <param name="destinationType">Destination runtime type.</param>
    /// <param name="map">Configured type map describing member mappings.</param>
    /// <returns>Mapped destination instance.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when constructor cannot be created.</exception>
    private object MapUsingTypeMap(object source, Type sourceType, Type destinationType, ITypeMap map)
    {
        if (source == null) throw new MapperArgumentNullException(nameof(source));
        if (sourceType == null) throw new MapperArgumentNullException(nameof(sourceType));
        if (destinationType == null) throw new MapperArgumentNullException(nameof(destinationType));
        if (map == null) throw new MapperArgumentNullException(nameof(map));

        // Use compiled ctor & property accessors to avoid reflection hotspots
        var ctor = ConstructorCache.GetOrAdd(destinationType, CreateConstructor);
        if (ctor == null) throw new MapperConfigurationException($"Failed to create constructor for type '{destinationType.FullName}'");

        var dest = ctor();
        if (dest == null) throw new MapperConfigurationException($"Constructor for type '{destinationType.FullName}' returned null");

        var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p != null && p.CanWrite).ToArray());

        var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p != null && p.CanRead).ToArray());

        var readableDict = readable.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var mapType = map.GetType();
        var tryGetMemberMapMethod = TryGetMemberMapMethodCache.GetOrAdd(mapType, t =>
            t.GetMethod("TryGetMemberMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));

        foreach (var destProp in writable)
        {
            if (destProp == null || destProp.Name == null) continue;

            try
            {
                IMemberMap? memberMap;
                if (tryGetMemberMapMethod != null)
                {
                    var args = new object?[] { destProp.Name, null! };
                    var invokeResult = tryGetMemberMapMethod.Invoke(map, args);
                    if (invokeResult != null)
                    {
                        var found = (bool)invokeResult;
                        memberMap = (IMemberMap?)args[1];
                        if (found && memberMap != null && memberMap.Ignored) continue;
                        if (found && memberMap != null && memberMap.Resolve != null)
                        {
                            var resolved = memberMap.Resolve(source);
                            var setter = SetterCache.GetOrAdd(destProp, CreateSetter);
                            if (setter == null) continue;

                            // Check if the resolved value needs mapping (e.g., collection mapping)
                            if (resolved != null)
                            {
                                var resolvedType = resolved.GetType();
                                // Try to map the resolved value if types don't match
                                if (!destProp.PropertyType.IsAssignableFrom(resolvedType))
                                {
                                    var mapped = Map(resolved, resolvedType, destProp.PropertyType);
                                    ApplySet(setter, dest, destProp.PropertyType, mapped);
                                    continue;
                                }
                            }

                            ApplySet(setter, dest, destProp.PropertyType, resolved);
                            continue;
                        }
                    }
                }

                if (!readableDict.TryGetValue(destProp.Name, out var srcProp)) continue;

                var getter = GetterCache.GetOrAdd(srcProp, CreateGetter);
                var setterForDest = SetterCache.GetOrAdd(destProp, CreateSetter);

                if (getter == null || setterForDest == null) continue;

                var val = getter(source);
                if (val != null && _config.TryGetMap(srcProp.PropertyType, destProp.PropertyType, out var nestedMap) && nestedMap != null)
                {
                    var mapped = Map(val, srcProp.PropertyType, destProp.PropertyType);
                    ApplySet(setterForDest, dest, destProp.PropertyType, mapped);
                }
                else
                {
                    ApplySet(setterForDest, dest, destProp.PropertyType, val);
                }
            }
            catch (MapperException)
            {
                // Re-throw mapper exceptions as-is
                throw;
            }
            catch (Exception ex)
            {
                // Wrap other exceptions with context information
                throw new MapperMappingException(
                    $"Error mapping property '{destProp.Name}' " +
                    $"from type '{sourceType.FullName}' to '{destinationType.FullName}'",
                    sourceType, destinationType, destProp.Name, ex);
            }
        }

        return dest;
    }

    /// <summary>
    /// Applies a value to a destination property using a compiled setter.
    /// Performs null and type checks and attempts conversion when types differ.
    /// </summary>
    /// <param name="setter">Compiled setter delegate created for the destination property.</param>
    /// <param name="destination">Destination instance to set the property on.</param>
    /// <param name="destPropType">Declared property type on the destination.</param>
    /// <param name="value">Value to assign to the property (may be null).</param>
    private static void ApplySet(Action<object, object?> setter, object destination, Type destPropType, object? value)
    {
        if (value is null)
        {
            if (destPropType.IsValueType && Nullable.GetUnderlyingType(destPropType) == null)
                return;
            setter(destination, null);
            return;
        }

        if (destPropType.IsInstanceOfType(value))
        {
            setter(destination, value);
            return;
        }

        try
        {
            var targetType = Nullable.GetUnderlyingType(destPropType) ?? destPropType;
            var converted = Convert.ChangeType(value, targetType);
            setter(destination, converted);
        }
        catch
        {
            // ignore failed conversions
        }
    }

    /// <summary>
    /// Creates a parameterless constructor delegate for the specified type.
    /// The returned delegate constructs a new instance and returns it as object.
    /// Handles records and types with parameterized constructors by using default values.
    /// </summary>
    /// <param name="type">Type to create a constructor delegate for.</param>
    /// <returns>Delegate that constructs and returns a new instance of <paramref name="type"/>.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when type is null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when type has no accessible constructor.</exception>
    private static Func<object> CreateConstructor(Type type)
    {
        if (type == null) 
            throw new MapperArgumentNullException(nameof(type));
        
        if (type.IsAbstract || type.IsInterface)
            throw new MapperConfigurationException($"Type '{type.FullName}' cannot be instantiated.");

        // Try to get parameterless constructor first
        var parameterlessConstructor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

        if (parameterlessConstructor != null)
        {
            var newExpression = Expression.New(parameterlessConstructor);
            return Expression.Lambda<Func<object>>(Expression.Convert(newExpression, typeof(object))).Compile();
        }

        // For records or types without parameterless constructor, find constructor with parameters
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors is null || constructors.Length == 0 )
            throw new MapperConfigurationException($"Type '{type.FullName ?? type.Name}' does not have an accessible constructor.");
        
        var constructor = constructors[0];
        if (constructor == null)
            throw new MapperConfigurationException($"Type '{type.FullName ?? type.Name}' does not have an accessible constructor.");
        
        var parameters = constructor.GetParameters();
        
        var arguments = parameters.Length > 0
            ? parameters.Select(p => Expression.Constant(GetDefaultFor(p.ParameterType), p.ParameterType)).ToArray()
            : Array.Empty<Expression>();

        var expression = Expression.New(constructor, arguments);
        return Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();
    }

    /// <summary>
    /// Creates a compiled property getter that accepts an object instance and returns the property value as object.
    /// </summary>
    /// <param name="propertyInfo">Property to create a getter for.</param>
    /// <returns>Getter delegate accepting object instance and returning property value boxed as object.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when prop is null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when property has no declaring type.</exception>
    private static Func<object, object?> CreateGetter(PropertyInfo propertyInfo)
    {
        if (propertyInfo == null) 
            throw new MapperArgumentNullException(nameof(propertyInfo));
        
        if (propertyInfo.DeclaringType == null)
            throw new MapperConfigurationException($"Property '{propertyInfo.Name}' has no declaring type");
        
        var instanceParameters = Expression.Parameter(typeof(object), Instance);
        var castInstance = Expression.Convert(instanceParameters, propertyInfo.DeclaringType);
        var propertyAccess = Expression.Property(castInstance, propertyInfo);
        var convertResult = Expression.Convert(propertyAccess, typeof(object));
        var lambda = Expression.Lambda<Func<object, object?>>(convertResult, instanceParameters);
        return lambda.Compile();
    }

    /// <summary>
    /// Creates a compiled property setter that accepts an object instance and an object value.
    /// The setter casts the instance and value to the correct types and assigns the property.
    /// </summary>
    /// <param name="propertyInfo">Property to create a setter for.</param>
    /// <returns>Setter delegate accepting destination instance and boxed value.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when prop is null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when property has no declaring type.</exception>
    private static Action<object, object?> CreateSetter(PropertyInfo propertyInfo)
    {
        if (propertyInfo == null) 
            throw new MapperArgumentNullException(nameof(propertyInfo));
        
        if (propertyInfo.DeclaringType == null)
            throw new MapperConfigurationException($"Property '{propertyInfo.Name}' has no declaring type");

        var instanceParameter = Expression.Parameter(typeof(object), Instance);
        var valueParameter = Expression.Parameter(typeof(object), Value);

        var castInstance = Expression.Convert(instanceParameter, propertyInfo.DeclaringType);

        var targetType = propertyInfo.PropertyType;
        var convertedValue = Expression.Convert(valueParameter, targetType);

        var propertySet = Expression.Assign(Expression.Property(castInstance, propertyInfo), convertedValue);
        // we create a lambda where instance is the destination instance passed by the caller.
        var lambda = Expression.Lambda<Action<object, object?>>(propertySet, instanceParameter, valueParameter);
        return lambda.Compile();
    }

    /// <summary>
    /// Tries to handle mapping of collections (IEnumerable). If both source and destination expose an
    /// element type, the method maps elements individually and returns a new List of destination element type.
    /// Optimized with caching, compiled expressions, and capacity pre-allocation.
    /// </summary>
    /// <param name="source">Source enumerable instance.</param>
    /// <param name="sourceType">Type of the source instance.</param>
    /// <param name="destinationType">Requested destination type.</param>
    /// <param name="result">Mapped collection result when method returns true.</param>
    /// <returns>True when collection mapping was handled; otherwise false.</returns>
    private bool TryHandleCollection(object source, Type sourceType, Type destinationType, out object? result)
    {
        result = null;

        if (source == null) return false;
        if (sourceType == null) return false;
        if (destinationType == null) return false;

        if (!typeof(IEnumerable).IsAssignableFrom(sourceType)) return false;

        // Use cached element type resolution
        var sourceElement = ElementTypeCache.GetOrAdd(sourceType, GetIEnumerableElementType);
        var destinationElement = ElementTypeCache.GetOrAdd(destinationType, GetIEnumerableElementType);
        if (sourceElement == null || destinationElement == null) return false;

        var sourceEnumerable = (IEnumerable)source;

        // Get or build compiled collection mapping delegate
        var key = (srcElem: sourceElement, dstElem: destinationElement);
        var collectionMapperLazy = _collectionMapDelegateCache.GetOrAdd(key, k => new Lazy<Func<IEnumerable, object>>(() => BuildCollectionMappingDelegate(k.srcElem, k.dstElem), LazyThreadSafetyMode.ExecutionAndPublication));

        if (collectionMapperLazy == null) return false;

        var collectionMapper = collectionMapperLazy.Value;

        result = collectionMapper(sourceEnumerable);
        return true;
    }

    /// <summary>
    /// Builds a compiled delegate for mapping collections from source element type to destination element type.
    /// Uses expression trees to create a strongly-typed, optimized mapping function with capacity pre-allocation.
    /// </summary>
    /// <param name="sourceElemType">Source element type.</param>
    /// <param name="destinationElemType">Destination element type.</param>
    /// <returns>Compiled function that maps IEnumerable to List of destination element type.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when element types are null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when list constructor cannot be created.</exception>
    private Func<IEnumerable, object> BuildCollectionMappingDelegate(Type sourceElemType, Type destinationElemType)
    {
        if (sourceElemType == null) throw new MapperArgumentNullException(nameof(sourceElemType));
        if (destinationElemType == null) throw new MapperArgumentNullException(nameof(destinationElemType));

        // Get or create compiled list constructor with capacity
        var listConstructor = ListConstructorCache.GetOrAdd(destinationElemType, CreateListConstructor);
        if (listConstructor == null)
            throw new MapperConfigurationException($"Failed to create list constructor for type '{destinationElemType.FullName}'");

        // Return a closure that efficiently maps collections
        return sourceEnumerable =>
        {
            if (sourceEnumerable == null)
                throw new MapperArgumentNullException(nameof(sourceEnumerable));

            // Try to get count for capacity pre-allocation
            int capacity = 0;
            if (sourceEnumerable is ICollection collection)
            {
                capacity = collection.Count;
            }

            // Create list with pre-allocated capacity (avoids resizing)
            var list = listConstructor(capacity);
            if (list == null)
                throw new MapperConfigurationException($"List constructor for type '{destinationElemType.FullName}' returned null");

            var typedList = (IList)list;

            // Map each element using the main Map method
            foreach (var item in sourceEnumerable)
            {
                try
                {
                    if (item != null)
                    {
                        var mapped = Map(item, sourceElemType, destinationElemType);
                        typedList.Add(mapped);
                    }
                    else
                    {
                        // Handle null items - add default value
                        typedList.Add(GetDefaultFor(destinationElemType));
                    }
                }
                catch (MapperException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new MapperMappingException(
                        $"Error mapping collection element from type '{sourceElemType.FullName}' to '{destinationElemType.FullName}'",
                        sourceElemType, destinationElemType, null, exception);
                }
            }

            return list;
        };
    }

    /// <summary>
    /// Creates a compiled constructor for List&lt;T&gt; that accepts an initial capacity parameter.
    /// Uses expression trees to avoid reflection overhead during list creation.
    /// </summary>
    /// <param name="elementType">Element type for the list.</param>
    /// <returns>Compiled function that creates a List with specified capacity.</returns>
    /// <exception cref="MapperArgumentNullException">Thrown when elementType is null.</exception>
    /// <exception cref="MapperConfigurationException">Thrown when list constructor cannot be created.</exception>
    private static Func<int, object> CreateListConstructor(Type elementType)
    {
        if (elementType == null) throw new MapperArgumentNullException(nameof(elementType));

        var listType = typeof(List<>).MakeGenericType(elementType);
        if (listType == null)
            throw new MapperConfigurationException($"Failed to create List<{elementType.Name}> type");

        var capacityParam = Expression.Parameter(typeof(int), "capacity");

        // Get the List<T>(int capacity) constructor
        var ctor = listType.GetConstructor(new[] { typeof(int) });
        if (ctor == null)
        {
            // Fallback to parameterless constructor if capacity constructor not found
            var parameterlessCtor = listType.GetConstructor(Type.EmptyTypes);
            if (parameterlessCtor == null)
                throw new MapperConfigurationException($"Cannot create List<{elementType.Name}> constructor - no suitable constructor found");

            var newExpr = Expression.New(parameterlessCtor);
            var convert = Expression.Convert(newExpr, typeof(object));
            var lambda = Expression.Lambda<Func<int, object>>(convert, capacityParam);
            return lambda.Compile();
        }

        var newExprWithCapacity = Expression.New(ctor, capacityParam);
        var convertWithCapacity = Expression.Convert(newExprWithCapacity, typeof(object));
        var lambdaWithCapacity = Expression.Lambda<Func<int, object>>(convertWithCapacity, capacityParam);
        return lambdaWithCapacity.Compile();
    }

    /// <summary>
    /// Attempts to determine the element type for types implementing IEnumerable&lt;T&gt; or arrays.
    /// Returns null when no element type can be resolved.
    /// </summary>
    /// <param name="t">Type to inspect.</param>
    /// <returns>Element type if found; otherwise null.</returns>
    private static Type? GetIEnumerableElementType(Type t)
    {
        if (t == null) return null;

        if (t.IsArray)
        {
            var elementType = t.GetElementType();
            return elementType; // Can be null for some edge cases
        }

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var args = t.GetGenericArguments();
            return (args != null && args.Length > 0) ? args[0] : null;
        }

        var interfaces = t.GetInterfaces();
        if (interfaces != null)
        {
            foreach (var iface in interfaces)
            {
                if (iface != null && iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var args = iface.GetGenericArguments();
                    return (args != null && args.Length > 0) ? args[0] : null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the default value for the specified type. For value types returns a new instance;
    /// for reference types returns null.
    /// </summary>
    /// <param name="t">Type to produce a default value for.</param>
    /// <returns>Default value for <paramref name="t"/>.</returns>
    private static object? GetDefaultFor(Type t)
    {
        if (t == null) return null;
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}

