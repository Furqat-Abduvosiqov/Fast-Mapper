using System.Linq.Expressions;
using Fast_Mapper.Core.Abstractions;
using Fast_Mapper.Core.Exceptions;
using Fast_Mapper.Core.Helpers;

namespace Fast_Mapper.Core.BaseLogic;

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
    
    private Func<TSource, TDestination>? _convertUsing;

    /// <summary>
    /// Provide a custom converter for the whole source -> destination.
    /// </summary>
    public TypeMap<TSource, TDestination> ConvertUsing(Func<TSource, TDestination> converter)
    {
        _convertUsing = converter ?? throw new MapperException(nameof(converter));
        return this;
    }

    internal bool TryGetConverter(out Func<TSource, TDestination>? converter)
    {
        converter = _convertUsing;
        return converter != null;
    }

    
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
    /// Attempts to get a member map for the specified destination property name.
    /// </summary>
    /// <param name="destName">The destination property name.</param>
    /// <param name="map">The member map if found, otherwise null.</param>
    /// <returns>True if the member map was found, otherwise false.</returns>
    internal bool TryGetMemberMap(string destName, out IMemberMap? map)
    {
        return _memberMaps.TryGetValue(destName, out map);
    }
}