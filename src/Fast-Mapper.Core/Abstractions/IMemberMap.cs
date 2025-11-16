namespace Fast_Mapper.Core.Abstractions;

/// <summary>
/// Member mapping configuration
/// </summary>
public interface IMemberMap
{
    /// <summary>
    /// Member name in source type
    /// </summary>
    string DestinationMemberName { get; }
    
    /// <summary>
    /// Is member ignored
    /// </summary>
    bool Ignored { get; }
    
    /// <summary>
    /// Resolve member value from source object
    /// </summary>
    object? Resolve(object source);
}