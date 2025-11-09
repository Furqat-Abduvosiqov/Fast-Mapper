namespace Fast_Mapper.Core.Exceptions;

/// <summary>
/// Exception thrown when a required argument is null.
/// </summary>
public class MapperArgumentNullException : MapperException
{
    public string? ParameterName { get; }

    public MapperArgumentNullException(string parameterName) 
        : base($"Value cannot be null. (Parameter '{parameterName}')")
    {
        ParameterName = parameterName;
    }

    public MapperArgumentNullException(string parameterName, string message) 
        : base(message)
    {
        ParameterName = parameterName;
    }
}

