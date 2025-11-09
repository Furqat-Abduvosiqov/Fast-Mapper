using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Fast_Mapper.Core.Abstractions;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Benchmarks;

/// <summary>
/// Benchmarks comparing optimized vs non-optimized approaches
/// to demonstrate the value of caching and expression compilation
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ComparisonBenchmarks
{
    private IMapper _cachedMapper = null!;
    private User _user = null!;
    private List<Contact> _contacts = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-configured mapper (warm cache)
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();
        cfg.CreateMap<Contact, ContactDto>();
        _cachedMapper = cfg.BuildMapper();
        
        _user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            AgeString = "30"
        };
        
        _contacts = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
        
        // Warm up the cache
        _cachedMapper.Map<User, UserDto>(_user);
        _cachedMapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Baseline = true, Description = "Optimized: Warm cache, compiled expressions")]
    public UserDto OptimizedMapping()
    {
        return _cachedMapper.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Cold start: New mapper instance each time")]
    public UserDto ColdStartMapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();
        var mapper = cfg.BuildMapper();
        return mapper.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Optimized: Collection with cached delegates")]
    public List<ContactDto> OptimizedCollectionMapping()
    {
        return (List<ContactDto>)_cachedMapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Cold start: Collection without cache")]
    public List<ContactDto> ColdStartCollectionMapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>();
        var mapper = cfg.BuildMapper();
        return (List<ContactDto>)mapper.Map(_contacts, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Optimized: 10 sequential mappings (cache benefit)")]
    public List<UserDto> OptimizedSequentialMappings()
    {
        var results = new List<UserDto>(10);
        for (int i = 0; i < 10; i++)
        {
            results.Add(_cachedMapper.Map<User, UserDto>(_user));
        }
        return results;
    }

    [Benchmark(Description = "Cold start: 10 sequential mappings (no cache)")]
    public List<UserDto> ColdStartSequentialMappings()
    {
        var results = new List<UserDto>(10);
        for (int i = 0; i < 10; i++)
        {
            var cfg = new MapperConfig();
            cfg.CreateMap<User, UserDto>();
            var mapper = cfg.BuildMapper();
            results.Add(mapper.Map<User, UserDto>(_user));
        }
        return results;
    }
}

