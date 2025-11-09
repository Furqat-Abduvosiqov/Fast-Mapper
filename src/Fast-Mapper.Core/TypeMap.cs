using System.Linq.Expressions;
using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core;

/// <summary>
/// 
/// </summary>
/// <typeparam name="TSource"></typeparam>
/// <typeparam name="TDestination"></typeparam>
public class TypeMap<TSource, TDestination> : ITypeMap
{
    public Type SourceType => typeof(TSource);
    public Type DestinationType => typeof(TDestination);

    private readonly Dictionary<string, IMemberMap> _memberMaps = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IMemberMap> MemberMaps => _memberMaps;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="destMember"></param>
    /// <param name="resolver"></param>
    /// <typeparam name="TMember"></typeparam>
    /// <returns></returns>
    public TypeMap<TSource, TDestination> ForMember<TMember>(Expression<Func<TDestination, TMember>> destMember, Func<TSource, object> resolver)
    {
        var name = ExpressionsHelper.GetMemberName(destMember);
        _memberMaps[name] = new MemberMap
        {
            DestinationMemberName = name,
            Resolver = (src) => resolver((TSource)src),
            Ignored = false
        };
        return this;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="destMember"></param>
    /// <typeparam name="TMember"></typeparam>
    /// <returns></returns>
    public TypeMap<TSource, TDestination> Ignore<TMember>(Expression<Func<TDestination, TMember>> destMember)
    {
        var name = ExpressionsHelper.GetMemberName(destMember);
        _memberMaps[name] = new MemberMap
        {
            DestinationMemberName = name,
            Resolver = null,
            Ignored = true
        };
        return this;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="destName"></param>
    /// <param name="map"></param>
    /// <returns></returns>
    internal bool TryGetMemberMap(string destName, out IMemberMap map)
    {
        return _memberMaps.TryGetValue(destName, out map);
    }
}