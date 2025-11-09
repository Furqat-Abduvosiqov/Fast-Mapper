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
public class CustomMappingBenchmarks
{
    private IMapper _mapperBasic = null!;
    private IMapper _mapperForMember = null!;
    private IMapper _mapperFlattening = null!;
    private IMapper _mapperConvertUsing = null!;
    private IMapper _mapperIgnore = null!;
    private User _user = null!;
    private Contact _contact = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Basic mapper
        var cfgBasic = new MapperConfig();
        cfgBasic.CreateMap<User, UserDto>();
        _mapperBasic = cfgBasic.BuildMapper();
        
        // ForMember mapper
        var cfgForMember = new MapperConfig();
        cfgForMember.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .ForMember(d => d.Age, s => int.Parse(s.AgeString ?? "0"));
        _mapperForMember = cfgForMember.BuildMapper();
        
        // Flattening mapper
        var cfgFlattening = new MapperConfig();
        cfgFlattening.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street!);
        _mapperFlattening = cfgFlattening.BuildMapper();
        
        // ConvertUsing mapper
        var cfgConvertUsing = new MapperConfig();
        cfgConvertUsing.CreateMap<Contact, ContactDto>()
            .ConvertUsing(c => new ContactDto(c.Type.ToUpper(), c.Value.ToLower()));
        _mapperConvertUsing = cfgConvertUsing.BuildMapper();
        
        // Ignore mapper
        var cfgIgnore = new MapperConfig();
        cfgIgnore.CreateMap<User, UserDto>()
            .Ignore(d => d.Password);
        _mapperIgnore = cfgIgnore.BuildMapper();
        
        _user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Password = "secret123",
            AgeString = "30",
            Address = new Address
            {
                Street = "123 Main St",
                City = "New York"
            }
        };
        
        _contact = new Contact("Email", "TEST@EXAMPLE.COM");
    }

    [Benchmark(Baseline = true, Description = "Basic mapping (no customization)")]
    public UserDto BasicMapping()
    {
        return _mapperBasic.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Mapping with ForMember")]
    public UserDto MappingWithForMember()
    {
        return _mapperForMember.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Mapping with flattening")]
    public UserDto MappingWithFlattening()
    {
        return _mapperFlattening.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Mapping with ConvertUsing")]
    public ContactDto MappingWithConvertUsing()
    {
        return _mapperConvertUsing.Map<Contact, ContactDto>(_contact);
    }

    [Benchmark(Description = "Mapping with Ignore")]
    public UserDto MappingWithIgnore()
    {
        return _mapperIgnore.Map<User, UserDto>(_user);
    }

    [Benchmark(Description = "Complex mapping (ForMember + Flattening)")]
    public UserDto ComplexMapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .ForMember(d => d.AddressStreet, s => s.Address?.Street!)
            .ForMember(d => d.Age, s => int.Parse(s.AgeString ?? "0"));
        var mapper = cfg.BuildMapper();
        return mapper.Map<User, UserDto>(_user);
    }
}

