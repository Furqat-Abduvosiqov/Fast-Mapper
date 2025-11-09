namespace Fast_Mapper.Core.Exceptions;

/// <summary>
/// Exception thrown when an error occurs during the mapping process.
/// </summary>
public class MapperMappingException : MapperException
{
    public Type? SourceType { get; }
    public Type? DestinationType { get; }
    public string? PropertyName { get; }

    public MapperMappingException(string message) : base(message) { }

    public MapperMappingException(string message, Exception inner) : base(message, inner) { }

    public MapperMappingException(string message, Type? sourceType, Type? destinationType) 
        : base(message)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }

    public MapperMappingException(string message, Type? sourceType, Type? destinationType, string? propertyName, Exception inner) 
        : base(message, inner)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
        PropertyName = propertyName;
    }
}

