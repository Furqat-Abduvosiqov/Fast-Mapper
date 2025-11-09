using System.Collections.Concurrent;
using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core.BaseLogic;

/// <summary>
/// Mapper configuration class
/// </summary>
public class MapperConfig
{
    private readonly ConcurrentDictionary<(Type src, Type dst), ITypeMap> _maps = new();
    
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TDestination"></typeparam>
    /// <returns></returns>
    public TypeMap<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var key = (typeof(TSource), typeof(TDestination));
        var map = new TypeMap<TSource, TDestination>();
        _maps[key] = map;
        return map;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dst"></param>
    /// <param name="map"></param>
    /// <returns></returns>
    public bool TryGetMap(Type src, Type dst, out ITypeMap map) => _maps.TryGetValue((src, dst), out map!);
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IMapper BuildMapper() => new Mapper(this);
}