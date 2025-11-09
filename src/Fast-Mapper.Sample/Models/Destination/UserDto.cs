namespace Fast_Mapper.Sample.Models.Destination;

public class UserDto
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public string? AddressStreet { get; set; }
    public string? Password { get; set; }
    public IEnumerable<ContactDto> Contacts { get; set; } = [];
    public int Age { get; set; }
}