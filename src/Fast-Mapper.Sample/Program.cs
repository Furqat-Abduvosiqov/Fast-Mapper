using Fast_Mapper.Core;

namespace Fast_Mapper.Sample;

public class User
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Address? Address { get; set; }
    public string? Password { get; set; }
}

public class Address { public string? Street { get; set; } public string? City { get; set; } }

public class UserDto
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public string? AddressStreet { get; set; }
    public string? Password { get; set; }
}

internal static class Program
{
    static void Main()
    {
        var cfg = new MapperConfig();
        
        cfg.CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
            .ForMember(dest => dest.AddressStreet, src => $"{src.Address?.Street ?? string.Empty} {src.Address?.City ?? string.Empty}")
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Password);

        var mapper = cfg.BuildMapper();

        var user = new User { Id = 1, FirstName = "John", LastName = "Doe", Address = new Address { Street = "Main", City = "City" }, Password = "secret" };
        var dto = mapper.Map<User, UserDto>(user);

        Console.WriteLine($"FullName: {dto.FullName}, Street: {dto.AddressStreet}, Id: {dto.Id} , Password: {dto.Password}");
    }
}