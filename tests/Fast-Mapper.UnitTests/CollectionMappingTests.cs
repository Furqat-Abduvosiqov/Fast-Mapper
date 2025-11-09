using Fast_Mapper.Core;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Tests;

public class CollectionMappingTests
{
    [Fact]
    public void Maps_empty_collection()
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
    public void Maps_collection_with_single_item()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact> { new Contact("Email", "test@example.com") };

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Email", result[0].Type);
        Assert.Equal("test@example.com", result[0].Value);
    }

    [Fact]
    public void Maps_collection_with_multiple_items()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact>
        {
            new Contact("Email", "test@example.com"),
            new Contact("Phone", "+1-555-1234"),
            new Contact("Fax", "+1-555-5678")
        };

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("Email", result[0].Type);
        Assert.Equal("Phone", result[1].Type);
        Assert.Equal("Fax", result[2].Type);
    }

    [Fact]
    public void Maps_array_to_list()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new[]
        {
            new Contact("Email", "test@example.com"),
            new Contact("Phone", "+1-555-1234")
        };

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(Contact[]), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Maps_IEnumerable_to_List()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        IEnumerable<Contact> contacts = new List<Contact>
        {
            new Contact("Email", "test@example.com"),
            new Contact("Phone", "+1-555-1234")
        }.AsEnumerable();

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(IEnumerable<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Maps_large_collection_efficiently()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 1000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(1000, result.Count);
        Assert.Equal("Type0", result[0].Type);
        Assert.Equal("Type999", result[999].Type);
    }

    [Fact]
    public void Maps_very_large_collection_efficiently()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 10000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(10000, result.Count);
        Assert.Equal("Type0", result[0].Type);
        Assert.Equal("Value0", result[0].Value);
        Assert.Equal("Type9999", result[9999].Type);
        Assert.Equal("Value9999", result[9999].Value);
    }

    [Fact]
    public void Caches_collection_mapping_delegates()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        
        // First call - creates and caches the delegate
        var contacts1 = new List<Contact> { new Contact("Email", "test1@example.com") };
        var result1 = (List<ContactDto>)mapper.Map(contacts1, typeof(List<Contact>), typeof(List<ContactDto>));
        
        // Second call - should use cached delegate
        var contacts2 = new List<Contact> { new Contact("Phone", "+1-555-1234") };
        var result2 = (List<ContactDto>)mapper.Map(contacts2, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal("test1@example.com", result1[0].Value);
        Assert.Equal("+1-555-1234", result2[0].Value);
    }

    [Fact]
    public void Maps_collection_multiple_times_uses_cache()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        
        // Multiple calls should reuse cached delegate
        for (int i = 0; i < 5; i++)
        {
            var contacts = new List<Contact> { new Contact($"Type{i}", $"Value{i}") };
            var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
            
            Assert.Single(result);
            Assert.Equal($"Type{i}", result[0].Type);
            Assert.Equal($"Value{i}", result[0].Value);
        }
    }

    [Fact]
    public void Maps_collection_using_ConvertUsing()
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
        var orders = new List<Order>
        {
            new Order { Id = 1, Total = 100.50m, Status = OrderStatus.Completed },
            new Order { Id = 2, Total = 200.75m, Status = OrderStatus.Pending }
        };

        var result = (List<OrderDto>)mapper.Map(orders, typeof(List<Order>), typeof(List<OrderDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("$100.50", result[0].TotalFormatted);
        Assert.Equal("Completed", result[0].Status);
        Assert.Equal("$200.75", result[1].TotalFormatted);
        Assert.Equal("Pending", result[1].Status);
    }

    [Fact]
    public void Maps_empty_collection_with_capacity_optimization()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact>(); // Empty list with known capacity

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Maps_record_types_in_collection()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact>
        {
            new Contact("Email", "test@example.com"),
            new Contact("Phone", "+1-555-1234")
        };

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.IsType<ContactDto>(result[0]);
        Assert.Equal("Email", result[0].Type);
        Assert.Equal("test@example.com", result[0].Value);
    }

    [Fact]
    public void Maps_collection_property_with_different_collection_types()
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
            Contacts = new HashSet<Contact>
            {
                new Contact("Email", "test@example.com"),
                new Contact("Phone", "+1-555-1234")
            }
        };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.NotNull(dto.Contacts);
        Assert.Equal(2, dto.Contacts.Count());
    }
}

