using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Fast_Mapper.Core.Abstractions;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class CollectionMappingBenchmarks
{
    private IMapper _mapper = null!;
    private List<Contact> _contacts10 = null!;
    private List<Contact> _contacts100 = null!;
    private List<Contact> _contacts1000 = null!;
    private List<Contact> _contacts10000 = null!;
    private List<User> _users100 = null!;
    private List<User> _users1000 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);
        
        _mapper = cfg.BuildMapper();
        
        // Setup collections of different sizes
        _contacts10 = Enumerable.Range(0, 10)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
            
        _contacts100 = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
            
        _contacts1000 = Enumerable.Range(0, 1000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
            
        _contacts10000 = Enumerable.Range(0, 10000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
            
        _users100 = Enumerable.Range(0, 100)
            .Select(i => new User
            {
                Id = i,
                FirstName = $"User{i}",
                LastName = "Test",
                Contacts = new List<Contact>
                {
                    new Contact("Email", $"user{i}@example.com"),
                    new Contact("Phone", $"+1-555-{i:D4}")
                }
            })
            .ToList();
            
        _users1000 = Enumerable.Range(0, 1000)
            .Select(i => new User
            {
                Id = i,
                FirstName = $"User{i}",
                LastName = "Test",
                Contacts = new List<Contact>
                {
                    new Contact("Email", $"user{i}@example.com"),
                    new Contact("Phone", $"+1-555-{i:D4}")
                }
            })
            .ToList();
    }

    [Benchmark(Description = "Map collection of 10 items")]
    public List<ContactDto> MapCollection10()
    {
        return (List<ContactDto>)_mapper.Map(_contacts10, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Map collection of 100 items")]
    public List<ContactDto> MapCollection100()
    {
        return (List<ContactDto>)_mapper.Map(_contacts100, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Map collection of 1,000 items")]
    public List<ContactDto> MapCollection1000()
    {
        return (List<ContactDto>)_mapper.Map(_contacts1000, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Map collection of 10,000 items")]
    public List<ContactDto> MapCollection10000()
    {
        return (List<ContactDto>)_mapper.Map(_contacts10000, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Map 100 objects with nested collections")]
    public List<UserDto> MapNestedCollections100()
    {
        return (List<UserDto>)_mapper.Map(_users100, typeof(List<User>), typeof(List<UserDto>));
    }

    [Benchmark(Description = "Map 1,000 objects with nested collections")]
    public List<UserDto> MapNestedCollections1000()
    {
        return (List<UserDto>)_mapper.Map(_users1000, typeof(List<User>), typeof(List<UserDto>));
    }

    [Benchmark(Description = "Map array to list")]
    public List<ContactDto> MapArrayToList()
    {
        var array = _contacts100.ToArray();
        return (List<ContactDto>)_mapper.Map(array, typeof(Contact[]), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Map IEnumerable to List")]
    public List<ContactDto> MapIEnumerableToList()
    {
        IEnumerable<Contact> enumerable = _contacts100;
        return (List<ContactDto>)_mapper.Map(enumerable, typeof(IEnumerable<Contact>), typeof(List<ContactDto>));
    }
}

