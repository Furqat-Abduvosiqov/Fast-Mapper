namespace Fast_Mapper.Core.Abstractions;

/// <summary>
/// Mapper interface
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Main mapping method which maps source object to destination object
    /// </summary>
    /// <param name="source"></param>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TDestination"></typeparam>
    /// <returns></returns>
    TDestination Map<TSource, TDestination>(TSource source);
    
    /// <summary>
    /// Mapping method which maps source object to destination object
    /// </summary>
    /// <param name="source"></param>
    /// <param name="sourceType"></param>
    /// <param name="destinationType"></param>
    /// <returns></returns>
    object Map(object source, Type sourceType, Type destinationType);
}