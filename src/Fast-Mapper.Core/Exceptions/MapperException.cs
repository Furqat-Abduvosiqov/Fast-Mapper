namespace Fast_Mapper.Core.Exceptions;

public class MapperException : Exception
{
    public MapperException(string message) : base(message) { }
    public MapperException(string message, Exception inner) : base(message, inner) { }
}