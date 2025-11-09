using System.Collections.Concurrent;
using System.Reflection;
using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core;

/// <summary>
/// Base mapper implementation
/// </summary>
public class Mapper : IMapper
{
    private readonly MapperConfig _config;

    // very simple cache for property lists and constructors
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> WritablePropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ReadablePropertiesCache = new();

    public Mapper(MapperConfig config)
    {
        _config = config;
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source is null) return default!;
        var result = (TDestination)Map(source, typeof(TSource), typeof(TDestination));
        return result;
    }

    /// <summary>
    /// Map object from sourceType to destinationType
    /// </summary>
    /// <param name="source">Original source object</param>
    /// <param name="sourceType">Source object's type</param>
    /// <param name="destinationType">Destination object's type</param>
    /// <returns></returns>
    public object Map(object? source, Type sourceType, Type destinationType)
    {
        if (source is null) return GetDefaultFor(destinationType)!;
        
        return _config.TryGetMap(sourceType, destinationType, out var typeMapObj) ?
            // dynamic dispatch to specialized logic for MemberMaps
            MapUsingTypeMap(source, sourceType, destinationType, typeMapObj) :
            // fallback: convention-based mapping
            MapByConvention(source, sourceType, destinationType);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    /// <param name="map"></param>
    /// <returns></returns>
    private object MapUsingTypeMap(object source, Type sourceType, Type destinationType, ITypeMap map)
    {
        // We'll use reflection to create destination instance
        var dest = Activator.CreateInstance(destinationType)!;

        var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanWrite).ToArray());

        var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead).ToArray());

        // if it's our TypeMap<TSource,TDestination>, we can get its member maps
        if (map is TypeMap<object, object> genericMapFallback)
        {
            // not reachable normally; but keep it safe
        }

        // Use reflection to access the typed TypeMap's TryGetMemberMap method
        var tryGetMemberMapMethod = map.GetType().GetMethod("TryGetMemberMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var destProp in writable)
        {
            // 1. check for explicit member map
            var args = new object?[] { destProp.Name, null! };
            bool found = (bool)tryGetMemberMapMethod!.Invoke(map, args)!;
            var memberMap = (IMemberMap?)args[1];

            switch (found)
            {
                case true when memberMap is { Ignored: true }:
                    continue; // skip
                case true when memberMap is not null && memberMap.Resolve != null:
                {
                    var val = memberMap.Resolve(source);
                    TrySetValue(dest, destProp, val);
                    continue;
                }
            }

            // 2. convention: find readable prop on source with same name
            var srcProp = readable.FirstOrDefault(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
            if (srcProp != null)
            {
                var val = srcProp.GetValue(source);
                // if nested mapping exists for property types, call Map recursively
                if (val != null && _config.TryGetMap(srcProp.PropertyType, destProp.PropertyType, out var nestedMap))
                {
                    var mapped = Map(val, srcProp.PropertyType, destProp.PropertyType);
                    TrySetValue(dest, destProp, mapped);
                }
                else
                {
                    TrySetValue(dest, destProp, val);
                }
            }
        }

        return dest;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    /// <returns></returns>
    private object MapByConvention(object source, Type sourceType, Type destinationType)
    {
        var dest = Activator.CreateInstance(destinationType)!;

        var writable = WritablePropertiesCache.GetOrAdd(destinationType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanWrite).ToArray());

        var readable = ReadablePropertiesCache.GetOrAdd(sourceType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead).ToArray());

        foreach (var destProp in writable)
        {
            var srcProp = readable.FirstOrDefault(p => string.Equals(p.Name, destProp.Name, StringComparison.OrdinalIgnoreCase));
            if (srcProp == null) continue;

            var val = srcProp.GetValue(source);
            if (val != null && _config.TryGetMap(srcProp.PropertyType, destProp.PropertyType, out var nestedMap))
            {
                var mapped = Map(val, srcProp.PropertyType, destProp.PropertyType);
                TrySetValue(dest, destProp, mapped);
            }
            else
            {
                TrySetValue(dest, destProp, val);
            }
        }

        return dest;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="destProp"></param>
    /// <param name="value"></param>
    private static void TrySetValue(object dest, PropertyInfo destProp, object? value)
    {
        if (value is null)
        {
            // if destination property is value type (non-nullable), skip or set default
            if (destProp.PropertyType.IsValueType && Nullable.GetUnderlyingType(destProp.PropertyType) == null)
            {
                // skip to avoid exception
                return;
            }

            destProp.SetValue(dest, null);
            return;
        }

        // try direct assign
        if (destProp.PropertyType.IsInstanceOfType(value))
        {
            destProp.SetValue(dest, value);
            return;
        }

        // try simple conversions (string -> int etc.)
        try
        {
            var converted = Convert.ChangeType(value, Nullable.GetUnderlyingType(destProp.PropertyType) ?? destProp.PropertyType);
            destProp.SetValue(dest, converted);
        }
        catch
        {
            // ignore failed conversions for now (could throw descriptive error later)
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private static object? GetDefaultFor(Type t)
    {
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}