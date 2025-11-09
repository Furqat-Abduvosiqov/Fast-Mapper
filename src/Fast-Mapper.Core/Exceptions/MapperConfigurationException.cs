namespace Fast_Mapper.Core.Exceptions;

/// <summary>
/// Exception thrown when mapper configuration is invalid or incomplete.
/// </summary>
public class MapperConfigurationException : MapperException
{
    public MapperConfigurationException(string message) : base(message) { }
    public MapperConfigurationException(string message, Exception inner) : base(message, inner) { }
}

