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
public class CachingBenchmarks
{
    private IMapper _mapper = null!;
    private User _user = null!;
    private List<Contact> _contacts = null!;

    [GlobalSetup]
    public void Setup()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();
        cfg.CreateMap<Contact, ContactDto>();
        
        _mapper = cfg.BuildMapper();
        
        _user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe"
        };
        
        _contacts = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
    }

    [Benchmark(Description = "First mapping (cold cache)")]
    public UserDto FirstMapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();
        var mapper = cfg.BuildMapper();
        return mapper.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Repeated mapping (warm cache)")]
    public List<UserDto> RepeatedMapping()
    {
        var results = new List<UserDto>(100);
        for (int i = 0; i < 100; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }

    [Benchmark(Description = "Collection mapping - first call")]
    public List<ContactDto> CollectionMappingFirstCall()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        var mapper = cfg.BuildMapper();
        return (List<ContactDto>)mapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Collection mapping - repeated calls")]
    public List<List<ContactDto>> CollectionMappingRepeatedCalls()
    {
        var results = new List<List<ContactDto>>(10);
        for (int i = 0; i < 10; i++)
        {
            results.Add((List<ContactDto>)_mapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>)));
        }
        return results;
    }

    [Benchmark(Description = "Delegate caching benefit")]
    public List<ContactDto> DelegateCachingBenefit()
    {
        // This should benefit from cached delegates
        return (List<ContactDto>)_mapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Constructor caching benefit")]
    public List<UserDto> ConstructorCachingBenefit()
    {
        var results = new List<UserDto>(1000);
        for (int i = 0; i < 1000; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }
}

