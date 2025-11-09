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
public class BasicMappingBenchmarks
{
    private IMapper _mapper = null!;
    private User _user = null!;
    private Contact _contact = null!;
    private Address _address = null!;

    [GlobalSetup]
    public void Setup()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>();
        cfg.CreateMap<Contact, ContactDto>();
        cfg.CreateMap<Address, AddressDto>();
        
        _mapper = cfg.BuildMapper();
        
        _user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Password = "secret123",
            AgeString = "30"
        };
        
        _contact = new Contact("Email", "test@example.com");
        
        _address = new Address
        {
            Street = "123 Main St",
            City = "New York"
        };
    }

    [Benchmark(Description = "Map simple object (User -> UserDto)")]
    public UserDto MapSimpleObject()
    {
        return _mapper.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Map record type (Contact -> ContactDto)")]
    public ContactDto MapRecordType()
    {
        return _mapper.Map<Contact, ContactDto>(_contact);
    }

    [Benchmark(Description = "Map object with nested properties")]
    public AddressDto MapNestedProperties()
    {
        return _mapper.Map<Address, AddressDto>(_address);
    }

    [Benchmark(Description = "Map 100 simple objects")]
    public List<UserDto> Map100Objects()
    {
        var results = new List<UserDto>(100);
        for (int i = 0; i < 100; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }

    [Benchmark(Description = "Map 1000 simple objects")]
    public List<UserDto> Map1000Objects()
    {
        var results = new List<UserDto>(1000);
        for (int i = 0; i < 1000; i++)
        {
            results.Add(_mapper.Map<User, UserDto>(_user));
        }
        return results;
    }
}

