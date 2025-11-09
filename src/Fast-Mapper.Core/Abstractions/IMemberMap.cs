namespace Fast_Mapper.Core.Abstractions;

/// <summary>
/// Member mapping configuration
/// </summary>
public interface IMemberMap
{
    string DestinationMemberName { get; }
    bool Ignored { get; }
    object? Resolve(object source);
}