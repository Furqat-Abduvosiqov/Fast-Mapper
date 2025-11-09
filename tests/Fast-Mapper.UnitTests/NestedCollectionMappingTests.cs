using Fast_Mapper.Core;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Tests;

public class NestedCollectionMappingTests
{
    [Fact]
    public void Maps_nested_collection_in_object()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            Contacts = new List<Contact>
            {
                new Contact("Email", "john@example.com"),
                new Contact("Phone", "+1-555-1234")
            }
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Contacts);
        Assert.Equal(2, dto.Contacts.Count());
        Assert.Equal("john@example.com", dto.Contacts.First().Value);
    }

    [Fact]
    public void Maps_nested_collections_in_collection()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User
            {
                Id = 1,
                FirstName = "John",
                Contacts = new List<Contact> { new Contact("Email", "john@example.com") }
            },
            new User
            {
                Id = 2,
                FirstName = "Jane",
                Contacts = new List<Contact> { new Contact("Phone", "+1-555-1234") }
            }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0].Contacts);
        Assert.Equal("john@example.com", result[0].Contacts.First().Value);
        Assert.Single(result[1].Contacts);
        Assert.Equal("+1-555-1234", result[1].Contacts.First().Value);
    }

    [Fact]
    public void Maps_empty_nested_collection()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            Contacts = new List<Contact>()
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Contacts);
        Assert.Empty(dto.Contacts);
    }

    [Fact]
    public void Maps_nested_collection_with_custom_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts.Where(c => c.Type == "Email"));

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            Contacts = new List<Contact>
            {
                new("Email", "test@example.com"),
                new("Phone", "+1-555-1234"),
                new("Email", "test2@example.com")
            }
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Contacts);
        Assert.Equal(2, dto.Contacts.Count());
        Assert.All(dto.Contacts, c => Assert.Equal("Email", c.Type));
    }

    [Fact]
    public void Maps_collection_with_nested_objects()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Address, AddressDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address != null ? s.Address.Street : null);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "John", Address = new Address { Street = "Main St", City = "NYC" } },
            new User { Id = 2, FirstName = "Jane", Address = null }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Main St", result[0].AddressStreet);
        Assert.Null(result[1].AddressStreet);
    }

    [Fact]
    public void Maps_collection_with_flattening()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address != null ? $"{s.Address.Street}, {s.Address.City}" : null);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "John", Address = new Address { Street = "Main St", City = "NYC" } },
            new User { Id = 2, FirstName = "Jane", Address = new Address { Street = "Second St", City = "LA" } }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Main St, NYC", result[0].AddressStreet);
        Assert.Equal("Second St, LA", result[1].AddressStreet);
    }

    [Fact]
    public void Maps_collection_with_ignored_property()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .Ignore(d => d.Password);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User { Id = 1, FirstName = "John", Password = "secret1" },
            new User { Id = 2, FirstName = "Jane", Password = "secret2" }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Null(result[0].Password);
        Assert.Null(result[1].Password);
    }

    [Fact]
    public void Maps_deeply_nested_collections()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var users = new List<User>
        {
            new User
            {
                Id = 1,
                FirstName = "John",
                Contacts = new List<Contact>
                {
                    new Contact("Email", "john1@example.com"),
                    new Contact("Phone", "+1-555-1111")
                }
            },
            new User
            {
                Id = 2,
                FirstName = "Jane",
                Contacts = new List<Contact>
                {
                    new Contact("Email", "jane@example.com"),
                    new Contact("Phone", "+1-555-2222"),
                    new Contact("Fax", "+1-555-3333")
                }
            }
        };

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Contacts.Count());
        Assert.Equal(3, result[1].Contacts.Count());
        Assert.Equal("john1@example.com", result[0].Contacts.First().Value);
        Assert.Equal("jane@example.com", result[1].Contacts.First().Value);
    }

    [Fact]
    public void Maps_nested_collection_with_large_dataset()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var users = Enumerable.Range(0, 100)
            .Select(i => new User
            {
                Id = i,
                FirstName = $"User{i}",
                Contacts = Enumerable.Range(0, 5)
                    .Select(j => new Contact($"Type{j}", $"Value{i}_{j}"))
                    .ToList()
            })
            .ToList();

        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));

        Assert.NotNull(result);
        Assert.Equal(100, result.Count);
        Assert.All(result, u => Assert.Equal(5, u.Contacts.Count()));
        Assert.Equal("Value0_0", result[0].Contacts.First().Value);
        Assert.Equal("Value99_4", result[99].Contacts.Last().Value);
    }
}

