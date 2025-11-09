using Fast_Mapper.Core;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;
using System.Diagnostics;
using Fast_Mapper.Core.BaseLogic;

namespace Fast_Mapper.Tests;

public class PerformanceAndCachingTests
{
    [Fact]
    public void Caches_element_type_resolution()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact> { new Contact("Email", "test@example.com") };

        // First call - caches element type
        var result1 = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
        
        // Second call - should use cached element type
        var result2 = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.Single(result1);
        Assert.Single(result2);
    }

    [Fact]
    public void Caches_list_constructor()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        
        // Multiple mappings should reuse cached list constructor
        for (int i = 0; i < 5; i++)
        {
            var contacts = new List<Contact> { new Contact($"Type{i}", $"Value{i}") };
            var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
            Assert.Single(result);
        }
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
        
        // Third call - should also use cached delegate
        var contacts3 = new List<Contact> { new Contact("Fax", "+1-555-5678") };
        var result3 = (List<ContactDto>)mapper.Map(contacts3, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Single(result3);
    }

    [Fact]
    public void Maps_large_collection_efficiently()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 10000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        var sw = Stopwatch.StartNew();
        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
        sw.Stop();

        Assert.Equal(10000, result.Count);
        // Performance assertion - should complete in reasonable time (adjust as needed)
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Mapping took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Pre_allocates_capacity_for_collections()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact>(1000); // Pre-allocated capacity
        for (int i = 0; i < 1000; i++)
        {
            contacts.Add(new Contact($"Type{i}", $"Value{i}"));
        }

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));

        Assert.Equal(1000, result.Count);
    }

    [Fact]
    public void Reuses_compiled_expressions()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName);

        var mapper = cfg.BuildMapper();
        
        // Multiple mappings should reuse compiled expressions
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            var user = new User { FirstName = $"User{i}", LastName = "Test" };
            var dto = mapper.Map<User, UserDto>(user);
        }
        sw.Stop();

        // Should be fast due to caching
        Assert.True(sw.ElapsedMilliseconds < 500, $"Mapping took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Handles_concurrent_mapping_efficiently()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        // Simulate concurrent mapping
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
            return result.Count;
        })).ToArray();

        Task.WaitAll(tasks);
        
        Assert.All(tasks, t => Assert.Equal(100, t.Result));
    }

    [Fact]
    public void Caches_property_getters_and_setters()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();

        var mapper = cfg.BuildMapper();
        
        // Multiple mappings should reuse cached getters/setters
        for (int i = 0; i < 100; i++)
        {
            var user = new User { Id = i, FirstName = $"User{i}" };
            var dto = mapper.Map<User, UserDto>(user);
            Assert.Equal(i, dto.Id);
        }
    }

    [Fact]
    public void Maps_nested_collections_efficiently()
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
                Contacts = Enumerable.Range(0, 10)
                    .Select(j => new Contact($"Type{j}", $"Value{i}_{j}"))
                    .ToList()
            })
            .ToList();

        var sw = Stopwatch.StartNew();
        var result = (List<UserDto>)mapper.Map(users, typeof(List<User>), typeof(List<UserDto>));
        sw.Stop();

        Assert.Equal(100, result.Count);
        Assert.All(result, u => Assert.Equal(10, u.Contacts.Count()));
        // Should complete in reasonable time
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Mapping took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Avoids_memory_allocation_with_capacity_pre_allocation()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        
        // Use ICollection to enable capacity pre-allocation
        ICollection<Contact> contacts = new List<Contact>(1000);
        for (int i = 0; i < 1000; i++)
        {
            contacts.Add(new Contact($"Type{i}", $"Value{i}"));
        }

        var result = (List<ContactDto>)mapper.Map(contacts, typeof(ICollection<Contact>), typeof(List<ContactDto>));

        Assert.Equal(1000, result.Count);
    }

    [Fact]
    public void Benchmarks_simple_mapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contact = new Contact("Email", "test@example.com");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            var dto = mapper.Map<Contact, ContactDto>(contact);
        }
        sw.Stop();

        // Should be very fast for simple mappings
        Assert.True(sw.ElapsedMilliseconds < 500, $"10000 mappings took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Benchmarks_collection_mapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
        }
        sw.Stop();

        // Should be fast with caching
        Assert.True(sw.ElapsedMilliseconds < 1000, $"100 collection mappings took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Handles_repeated_mapping_without_performance_degradation()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact> { new Contact("Email", "test@example.com") };

        var times = new List<long>();
        
        // Map multiple times and measure
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = (List<ContactDto>)mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>));
            sw.Stop();
            times.Add(sw.ElapsedTicks);
        }

        // Later mappings should not be significantly slower than first mapping
        var firstTime = times[0];
        var lastTime = times[^1];
        Assert.True(lastTime <= firstTime * 2, "Performance degraded over repeated mappings");
    }
}

