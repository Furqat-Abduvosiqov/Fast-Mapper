using Fast_Mapper.Core;
using Fast_Mapper.Sample;

namespace Fast_Mapper.Tests;

public class MapperTests
{
    [Fact]
    public void Maps_basic_properties_and_for_member()
    {
        var cfg = new MapperConfig();
        cfg.CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .ForMember(d => d.AddressStreet, s => s.Address?.Street ?? string.Empty);

        var mapper = cfg.BuildMapper();
        var user = new User { FirstName = "A", LastName = "B", Address = new Address { Street = "S" } };

        var dto = mapper.Map<User, UserDto>(user);

        Assert.Equal("A B", dto.FullName);
        Assert.Equal("S", dto.AddressStreet);
    }
}