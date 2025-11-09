using System.Diagnostics.CodeAnalysis;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Tests;

[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
public class NullHandlingTests
{
    [Fact]
    public void Handles_null_source_property()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street!);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.AddressStreet);
    }

    [Fact]
    public void Handles_null_nested_property()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street ?? "Unknown");

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Unknown", dto.AddressStreet);
    }

    [Fact]
    public void Handles_null_string_properties()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 1, FirstName = null, LastName = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.FullName);
    }

    [Fact]
    public void Handles_null_collection_property()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts != null ? s.Contacts : Enumerable.Empty<Contact>());

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 1, FirstName = "John", Contacts = null! };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Contacts);
        Assert.Empty(dto.Contacts);
    }

    [Fact]
    public void Handles_empty_collection()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact>();

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Handles_null_elements_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Address, AddressDto>();

        var mapper = cfg.BuildMapper();
        var addresses = new List<Address?>
        {
            new Address { Street = "Main St", City = "NYC" },
            null,
            new Address { Street = "Second St", City = "LA" }
        };

        // Filter out nulls before mapping
        var result = mapper.Map(addresses.Where(a => a != null).Cast<Address>(), 
            typeof(IEnumerable<Address>), typeof(List<AddressDto>));

        Assert.NotNull(result);
        var list = (List<AddressDto>)result;
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Handles_default_values()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        var user = new User(); // All properties default

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(0, dto.Id);
        Assert.Equal(0, dto.Age);
        Assert.Null(dto.FullName);
    }

    [Fact]
    public void Handles_null_in_custom_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName != null && s.LastName != null 
                ? $"{s.FirstName} {s.LastName}" 
                : s.FirstName ?? s.LastName ?? "Unknown");

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = null, LastName = "Doe" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Doe", dto.FullName);
    }

    [Fact]
    public void Handles_null_with_type_conversion()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => int.TryParse(s.AgeString, out var age) ? age : 0);

        var mapper = cfg.BuildMapper();
        var user = new User { AgeString = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(0, dto.Age);
    }

    [Fact]
    public void Handles_empty_string_properties()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "", LastName = "" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(" ", dto.FullName);
    }

    [Fact]
    public void Handles_whitespace_strings()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => string.Join(' ', new[] { s.FirstName, s.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x))));

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "  ", LastName = "Doe" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Doe", dto.FullName);
    }

    [Fact]
    public void Handles_null_in_collection_mapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new() { Id = 1, FirstName = "John", Contacts = new List<Contact> { new("Email", "john@example.com") } },
            new() { Id = 2, FirstName = "Jane", Contacts = new List<Contact>() }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Contacts);
        Assert.Empty(result[1].Contacts);
    }

    [Fact]
    public void Handles_null_address_in_collection()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street!);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "John", Address = new Address { Street = "Main St" } },
            new User { Id = 2, FirstName = "Jane", Address = null }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Main St", result[0].AddressStreet);
        Assert.Null(result[1].AddressStreet);
    }

    [Fact]
    public void Handles_null_coalescing_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street ?? "No Address");

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("No Address", dto.AddressStreet);
    }

    [Fact]
    public void Handles_multiple_null_properties()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName ?? "Unknown")
            .ForMember(d => d.AddressStreet, s => s.Address?.Street ?? "Unknown");

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = null, Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Unknown", dto.FullName);
        Assert.Equal("Unknown", dto.AddressStreet);
    }
}

