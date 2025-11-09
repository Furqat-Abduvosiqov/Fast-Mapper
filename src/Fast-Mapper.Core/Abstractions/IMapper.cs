namespace Fast_Mapper.Core.Abstractions;

/// <summary>
/// Mapper interface
/// </summary>
public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);
    object Map(object source, Type sourceType, Type destinationType);
}