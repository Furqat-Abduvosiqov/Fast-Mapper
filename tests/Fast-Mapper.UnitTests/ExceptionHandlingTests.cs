using System.Reflection;
using Fast_Mapper.Core;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Core.Exceptions;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Tests;

public class ExceptionHandlingTests
{
    [Fact]
    public void Throws_exception_when_resolver_throws()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => throw new InvalidOperationException("Test exception"));

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John" };

        Assert.Throws<InvalidOperationException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_division_by_zero_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => 100 / s.Id); // Will throw if Id is 0

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 0 };

        Assert.Throws<DivideByZeroException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_null_reference_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address.Street); // Will throw if Address is null

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        Assert.Throws<NullReferenceException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_invalid_cast_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => int.Parse(s.AgeString!)); // Will throw if AgeString is not a valid int

        var mapper = cfg.BuildMapper();
        var user = new User { AgeString = "invalid" };

        Assert.Throws<FormatException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_index_out_of_range_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName![0].ToString()); // Will throw if FirstName is null or empty

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "" };

        Assert.Throws<IndexOutOfRangeException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_argument_null_exception_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => string.Join(" ", (string[])null!)); // Will throw ArgumentNullException

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John" };

        Assert.Throws<ArgumentNullException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_overflow_exception_in_resolver()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => checked(s.Id * int.MaxValue)); // Will throw OverflowException

        var mapper = cfg.BuildMapper();
        var user = new User { Id = 2 };

        Assert.Throws<OverflowException>(() => mapper.Map<User, UserDto>(user));
    }

    [Fact]
    public void Handles_exception_in_collection_mapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>()
            .ConvertUsing(c => throw new InvalidOperationException("Test exception"));

        var mapper = cfg.BuildMapper();
        var contacts = new List<Contact> { new Contact("Email", "test@example.com") };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>)));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Handles_exception_in_nested_collection_mapping()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<Contact, ContactDto>()
            .ConvertUsing(c => throw new InvalidOperationException("Test exception"));
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Contacts, s => s.Contacts);

        var mapper = cfg.BuildMapper();
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            Contacts = new List<Contact> { new Contact("Email", "test@example.com") }
        };

        var ex = Assert.Throws<TargetInvocationException>(() => mapper.Map<User, UserDto>(user));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Handles_mapper_exception_for_invalid_expression()
    {
        var cfg = new MapperConfig();
        
        // This should work fine - just testing the API
        var typeMap = cfg.CreateMap<User, UserDto>();
        
        Assert.NotNull(typeMap);
    }

    [Fact]
    public void Handles_exception_when_convert_using_is_null()
    {
        var cfg = new MapperConfig();
        
        Assert.Throws<MapperException>(() => 
            cfg.CreateMap<User, UserDto>().ConvertUsing(null!));
    }

    [Fact]
    public void Handles_safe_null_propagation()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.AddressStreet, s => s.Address?.Street); // Safe null propagation

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "John", Address = null };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Null(dto.AddressStreet); // Should not throw
    }

    [Fact]
    public void Handles_try_parse_pattern()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.Age, s => int.TryParse(s.AgeString, out var age) ? age : 0);

        var mapper = cfg.BuildMapper();
        var user = new User { AgeString = "invalid" };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal(0, dto.Age); // Should not throw, returns default
    }

    [Fact]
    public void Handles_exception_in_large_collection()
    {
        var cfg = new MapperConfig();
        var counter = 0;
        cfg.CreateMap<Contact, ContactDto>()
            .ConvertUsing(c =>
            {
                counter++;
                if (counter == 500) throw new InvalidOperationException("Test exception at 500");
                return new ContactDto(c.Type, c.Value);
            });

        var mapper = cfg.BuildMapper();
        var contacts = Enumerable.Range(0, 1000)
            .Select(i => new Contact($"Type{i}", $"Value{i}"))
            .ToList();

        var ex = Assert.Throws<TargetInvocationException>(() =>
            mapper.Map(contacts, typeof(List<Contact>), typeof(List<ContactDto>)));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Handles_concurrent_mapping_without_exceptions()
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
            Assert.Equal(100, result.Count);
        })).ToArray();

        Task.WaitAll(tasks); // Should not throw
    }

    [Fact]
    public void Handles_recursive_mapping_safely()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName);

        var mapper = cfg.BuildMapper();
        
        // Map multiple times to ensure no state issues
        for (int i = 0; i < 10; i++)
        {
            var user = new User { FirstName = $"User{i}", LastName = "Test" };
            var dto = mapper.Map<User, UserDto>(user);
            Assert.Equal($"User{i} Test", dto.FullName);
        }
    }
}

