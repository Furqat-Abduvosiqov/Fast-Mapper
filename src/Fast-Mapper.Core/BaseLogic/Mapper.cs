using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core.BaseLogic
{
    /// <summary>
    /// Base mapper implementation (optimized)
    /// </summary>
    public class Mapper : IMapper
    {
        private readonly MapperConfig _config;

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
        private readonly ConcurrentDictionary<(Type src, Type dst), Func<object, object>> _mapDelegateCache = new();

        // Compiled collection mapping delegates: Func<IEnumerable source, List<TDest>>
        private readonly ConcurrentDictionary<(Type srcElem, Type dstElem), Func<System.Collections.IEnumerable, object>> _collectionMapDelegateCache = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapper"/> class with the specified configuration.
        /// </summary>
        /// <param name="config">Mapper configuration used to resolve type maps and converters.</param>
        public Mapper(MapperConfig config)
        {
            _config = config;
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
        public object Map(object source, Type sourceType, Type destinationType)
        {
            if (source == null) return GetDefaultFor(destinationType)!;

            // If we have a TypeMap and a converter, use it.
            if (_config.TryGetMap(sourceType, destinationType, out var typeMapObj))
            {
                var tryGetConverterMethod = typeMapObj.GetType().GetMethod("TryGetConverter",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (tryGetConverterMethod != null)
                {
                    var args = new object?[] { null };
                    var hasConverter = (bool)tryGetConverterMethod.Invoke(typeMapObj, args)!;
                    var converterObj = args[0];
                    if (hasConverter && converterObj != null)
                    {
                        var converterDelegate = (Delegate)converterObj;
                        return converterDelegate.DynamicInvoke(source)!;
                    }
                }

                if (TryHandleCollection(source, sourceType, destinationType, out var collResult))
                    return collResult;

                return MapUsingTypeMap(source, sourceType, destinationType, typeMapObj);
            }

            if (TryHandleCollection(source, sourceType, destinationType, out var collectionResult))
                return collectionResult;

            // Convention-based mapping: use or build cached delegate
            var key = (src: sourceType, dst: destinationType);
            var mapDelegate = _mapDelegateCache.GetOrAdd(key, k => BuildMappingDelegate(k.src, k.dst));
            return mapDelegate(source);
        }

        /// <summary>
        /// Builds and returns a compiled mapping delegate that maps an object of <paramref name="sourceType"/>
        /// to a new instance of <paramref name="destinationType"/> using cached constructor, getters and setters.
        /// </summary>
        /// <param name="sourceType">Source runtime type.</param>
        /// <param name="destinationType">Destination runtime type.</param>
        /// <returns>A function that accepts a source object and returns the mapped destination object.</returns>
        private Func<object, object> BuildMappingDelegate(Type sourceType, Type destinationType)
        {
            // prepare ctor
            var ctor = ConstructorCache.GetOrAdd(destinationType, CreateConstructor);

            // collect readable and writable props
            var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.CanWrite).ToArray());

            var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.CanRead).ToArray());

            // prepare getter/setter delegates for matching props
            var propPairs = new List<(Func<object, object?> getter, Action<object, object?> setter, Type srcPropType, Type dstPropType)>();

            foreach (var dstProp in writable)
            {
                var srcProp = readable.FirstOrDefault(p => string.Equals(p.Name, dstProp.Name, StringComparison.OrdinalIgnoreCase));
                if (srcProp == null) continue;

                var getter = GetterCache.GetOrAdd(srcProp, CreateGetter);
                var setter = SetterCache.GetOrAdd(dstProp, CreateSetter);
                propPairs.Add((getter, setter, srcProp.PropertyType, dstProp.PropertyType));
            }

            // return a closure that uses cached delegates and recursive Map when nested mapping needed
            return sourceObj =>
            {
                var dest = ctor();
                foreach (var (getter, setter, srcPropType, dstPropType) in propPairs)
                {
                    var val = getter(sourceObj);
                    if (val != null && _config.TryGetMap(srcPropType, dstPropType, out var nestedMap))
                    {
                        var mapped = Map(val, srcPropType, dstPropType);
                        ApplySet(setter, dest, dstPropType, mapped);
                    }
                    else
                    {
                        ApplySet(setter, dest, dstPropType, val);
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
        private object MapUsingTypeMap(object source, Type sourceType, Type destinationType, ITypeMap map)
        {
            // Use compiled ctor & property accessors to avoid reflection hotspots
            var dest = ConstructorCache.GetOrAdd(destinationType, CreateConstructor)();

            var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.CanWrite).ToArray());

            var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.CanRead).ToArray());

            var tryGetMemberMapMethod = map.GetType().GetMethod("TryGetMemberMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var destProp in writable)
            {
                IMemberMap? memberMap = null;
                if (tryGetMemberMapMethod != null)
                {
                    var args = new object?[] { destProp.Name, null! };
                    var found = (bool)tryGetMemberMapMethod.Invoke(map, args)!;
                    memberMap = (IMemberMap?)args[1];
                    if (found && memberMap != null && memberMap.Ignored) continue;
                    if (found && memberMap != null && memberMap.Resolve != null)
                    {
                        var resolved = memberMap.Resolve(source);
                        var setter = SetterCache.GetOrAdd(destProp, CreateSetter);

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

                var srcProp = readable.FirstOrDefault(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
                if (srcProp == null) continue;

                var getter = GetterCache.GetOrAdd(srcProp, CreateGetter);
                var setterForDest = SetterCache.GetOrAdd(destProp, CreateSetter);
                var val = getter(source);
                if (val != null && _config.TryGetMap(srcProp.PropertyType, destProp.PropertyType, out var nestedMap))
                {
                    var mapped = Map(val, srcProp.PropertyType, destProp.PropertyType);
                    ApplySet(setterForDest, dest, destProp.PropertyType, mapped);
                }
                else
                {
                    ApplySet(setterForDest, dest, destProp.PropertyType, val);
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
        /// <param name="t">Type to create a constructor delegate for.</param>
        /// <returns>Delegate that constructs and returns a new instance of <paramref name="t"/>.</returns>
        private static Func<object> CreateConstructor(Type t)
        {
            // Try to get parameterless constructor first
            var parameterlessCtor = t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if (parameterlessCtor != null)
            {
                var newExpr = Expression.New(parameterlessCtor);
                var convert = Expression.Convert(newExpr, typeof(object));
                var lambda = Expression.Lambda<Func<object>>(convert);
                return lambda.Compile();
            }

            // For records or types without parameterless constructor, find constructor with parameters
            var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length > 0)
            {
                // Use the first constructor and provide default values for parameters
                var ctor = ctors[0];
                var parameters = ctor.GetParameters();
                var defaultArgs = parameters.Select(p => Expression.Constant(GetDefaultFor(p.ParameterType), p.ParameterType)).ToArray();
                var newExpr = Expression.New(ctor, defaultArgs);
                var convert = Expression.Convert(newExpr, typeof(object));
                var lambda = Expression.Lambda<Func<object>>(convert);
                return lambda.Compile();
            }

            // Fallback: throw exception
            throw new InvalidOperationException($"Type '{t.FullName}' does not have an accessible constructor.");
        }

        /// <summary>
        /// Creates a compiled property getter that accepts an object instance and returns the property value as object.
        /// </summary>
        /// <param name="prop">Property to create a getter for.</param>
        /// <returns>Getter delegate accepting object instance and returning property value boxed as object.</returns>
        private static Func<object, object?> CreateGetter(PropertyInfo prop)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(instanceParam, prop.DeclaringType!);
            var propAccess = Expression.Property(castInstance, prop);
            var convertResult = Expression.Convert(propAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object, object?>>(convertResult, instanceParam);
            return lambda.Compile();
        }

        /// <summary>
        /// Creates a compiled property setter that accepts an object instance and an object value.
        /// The setter casts the instance and value to the correct types and assigns the property.
        /// </summary>
        /// <param name="prop">Property to create a setter for.</param>
        /// <returns>Setter delegate accepting destination instance and boxed value.</returns>
        private static Action<object, object?> CreateSetter(PropertyInfo prop)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var castInstance = Expression.Convert(instanceParam, prop.DeclaringType!);

            var targetType = prop.PropertyType;
            var convertedValue = Expression.Convert(valueParam, targetType);

            var propertySet = Expression.Assign(Expression.Property(castInstance, prop), convertedValue);
            // we create a lambda where instance is the destination instance passed by the caller.
            var lambda = Expression.Lambda<Action<object, object?>>(propertySet, instanceParam, valueParam);
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
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(sourceType)) return false;

            // Use cached element type resolution
            var srcElem = ElementTypeCache.GetOrAdd(sourceType, GetIEnumerableElementType);
            var dstElem = ElementTypeCache.GetOrAdd(destinationType, GetIEnumerableElementType);
            if (srcElem == null || dstElem == null) return false;

            var srcEnum = (System.Collections.IEnumerable)source;

            // Get or build compiled collection mapping delegate
            var key = (srcElem, dstElem);
            var collectionMapper = _collectionMapDelegateCache.GetOrAdd(key, k => BuildCollectionMappingDelegate(k.srcElem, k.dstElem));

            result = collectionMapper(srcEnum);
            return true;
        }

        /// <summary>
        /// Builds a compiled delegate for mapping collections from source element type to destination element type.
        /// Uses expression trees to create a strongly-typed, optimized mapping function with capacity pre-allocation.
        /// </summary>
        /// <param name="srcElemType">Source element type.</param>
        /// <param name="dstElemType">Destination element type.</param>
        /// <returns>Compiled function that maps IEnumerable to List of destination element type.</returns>
        private Func<System.Collections.IEnumerable, object> BuildCollectionMappingDelegate(Type srcElemType, Type dstElemType)
        {
            // Get or create compiled list constructor with capacity
            var listCtor = ListConstructorCache.GetOrAdd(dstElemType, CreateListConstructor);

            // Return a closure that efficiently maps collections
            return sourceEnum =>
            {
                // Try to get count for capacity pre-allocation
                int capacity = 0;
                if (sourceEnum is System.Collections.ICollection collection)
                {
                    capacity = collection.Count;
                }

                // Create list with pre-allocated capacity (avoids resizing)
                var list = listCtor(capacity);
                var typedList = (System.Collections.IList)list;

                // Map each element using the main Map method
                foreach (var item in sourceEnum)
                {
                    if (item != null)
                    {
                        var mapped = Map(item, srcElemType, dstElemType);
                        typedList.Add(mapped);
                    }
                    else
                    {
                        // Handle null items - add default value
                        typedList.Add(GetDefaultFor(dstElemType));
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
        private static Func<int, object> CreateListConstructor(Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var capacityParam = Expression.Parameter(typeof(int), "capacity");

            // Get the List<T>(int capacity) constructor
            var ctor = listType.GetConstructor(new[] { typeof(int) });
            if (ctor == null)
            {
                // Fallback to parameterless constructor if capacity constructor not found
                var parameterlessCtor = listType.GetConstructor(Type.EmptyTypes);
                var newExpr = Expression.New(parameterlessCtor!);
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
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return t.GetGenericArguments()[0];

            foreach (var iface in t.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
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
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }
    }
}
