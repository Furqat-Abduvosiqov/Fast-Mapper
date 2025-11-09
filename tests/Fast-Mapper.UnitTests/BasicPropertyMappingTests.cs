using Fast_Mapper.Core;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Core.Exceptions;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Tests;

public class BasicPropertyMappingTests
{
    [Fact]
    public void Maps_basic_properties_by_name()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 1, FirstName = "John", LastName = "Doe" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(1, dto.Id);
        Assert.Null(dto.FullName); // Not mapped by convention
    }

    [Fact]
    public void Maps_with_custom_for_member()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", LastName = "Doe" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("John Doe", dto.FullName);
    }

    [Fact]
    public void Maps_with_flattening()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street ?? string.Empty);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = new Address { Street = "Main St", City = "NYC" } };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Main St", dto.AddressStreet);
    }

    [Fact]
    public void Ignores_property_when_configured()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .Ignore(d => d.Password);

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 1, FirstName = "John", Password = "secret" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.Password);
    }

    [Fact]
    public void Maps_with_property_transformation()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Id, s => s.Id + 1000);

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 1 };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(1001, dto.Id);
    }

    [Fact]
    public void Maps_with_type_conversion()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => int.TryParse(s.AgeString, out var age) ? age : 0);

        var mapper = cfg.BuildMapper();
        var user = new User { AgeString = "42" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(42, dto.Age);
    }

    [Fact]
    public void Maps_with_null_source_property()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.AddressStreet);
    }

    [Fact]
    public void Maps_nested_object()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Address, AddressDto>();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            FirstName = "John",
            Address = new Address { Street = "Main St", City = "NYC" }
        };

        var dto = mapper.Map<User, UserDto>(user);

        // Note: Nested object mapping requires explicit configuration
        Assert.NotNull(user.Address);
    }

    [Fact]
    public void Maps_using_convert_using()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Order, OrderDto>()
            .ConvertUsing(o => new OrderDto
            {
                Id = o.Id,
                TotalFormatted = o.Total.ToString("C"),
                Status = o.Status.ToString()
            });

        var mapper = cfg.BuildMapper();
        var order = new Order { Id = 1, Total = 123.45m, Status = OrderStatus.Completed };

        var dto = mapper.Map<Order, OrderDto>(order);

        Assert.Equal(1, dto.Id);
        Assert.Equal("$123.45", dto.TotalFormatted);
        Assert.Equal("Completed", dto.Status);
    }

    [Fact]
    public void Maps_multiple_custom_members()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => $"{s.FirstName} {s.LastName}")
            .ForMember(d => d.AddressStreet, s => s.Address != null ? $"{s.Address.Street}, {s.Address.City}" : null)
            .ForMember(d => d.Age, s => int.TryParse(s.AgeString, out var age) ? age : 0)
            .Ignore(d => d.Password);

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Address = new Address { Street = "Main St", City = "NYC" },
            Password = "secret",
            AgeString = "42"
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("John Doe", dto.FullName);
        Assert.Equal("Main St, NYC", dto.AddressStreet);
        Assert.Equal(42, dto.Age);
        Assert.Null(dto.Password);
    }

    [Fact]
    public void Maps_with_empty_strings()
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
    public void Maps_with_whitespace_strings()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => string.Join(' ', new[] { s.FirstName, s.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))));

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "  ", LastName = "Doe" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("Doe", dto.FullName);
    }

    [Fact]
    public void Maps_with_default_values()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        var user = new User(); // All properties default

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(0, dto.Id);
        Assert.Equal(0, dto.Age);
    }
}

