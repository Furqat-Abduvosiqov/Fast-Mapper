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
public class MemoryAllocationBenchmarks
{
    private IMapper _mapper = null!;
    private User _user = null!;
    private List<Contact> _contacts100 = null!;
    private List<Contact> _contacts1000 = null!;

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
        
        _contacts100 = Enumerable.Range(0, 100)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
            
        _contacts1000 = Enumerable.Range(0, 1000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();
    }

    [Benchmark(Description = "Single object mapping allocation")]
    public UserDto SingleObjectAllocation()
    {
        return _mapper.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "100 objects mapping allocation")]
    public List<UserDto> Mapping100ObjectsAllocation()
    {
        var results = new List<UserDto>(100);
        for (int i = 0; i < 100; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }

    [Benchmark(Description = "Collection of 100 items allocation")]
    public List<ContactDto> Collection100Allocation()
    {
        return (List<ContactDto>)_mapper.Map(_contacts100, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Collection of 1000 items allocation")]
    public List<ContactDto> Collection1000Allocation()
    {
        return (List<ContactDto>)_mapper.Map(_contacts1000, typeof(List<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Pre-allocated capacity benefit")]
    public List<ContactDto> PreAllocatedCapacityBenefit()
    {
        // The mapper should pre-allocate capacity based on source collection count
        ICollection<Contact> contacts = _contacts100;
        return (List<ContactDto>)_mapper.Map(contacts, typeof(ICollection<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Without pre-allocation (IEnumerable)")]
    public List<ContactDto> WithoutPreAllocation()
    {
        // Using IEnumerable doesn't allow pre-allocation
        IEnumerable<Contact> contacts = _contacts100;
        return (List<ContactDto>)_mapper.Map(contacts, typeof(IEnumerable<Contact>), typeof(List<ContactDto>));
    }

    [Benchmark(Description = "Repeated mapping memory efficiency")]
    public List<UserDto> RepeatedMappingMemoryEfficiency()
    {
        var results = new List<UserDto>(1000);
        for (int i = 0; i < 1000; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }
}

