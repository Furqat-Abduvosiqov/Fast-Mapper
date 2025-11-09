using Fast_Mapper.Core;
using Fast_Mapper.Core.BaseLogic;
using Fast_Mapper.Sample.Models.Destination;
using Fast_Mapper.Sample.Models.Source;

namespace Fast_Mapper.Sample;

internal static class Program
{
    static void Main()
    {
        var cfg = new MapperConfig();

        // 1) Convention-based mapping for Contact -> ContactDto (positional record properties map by name)
        cfg.CreateMap<Contact, ContactDto>();

        // 2) Simple mapping for Address -> AddressDto (same names)
        cfg.CreateMap<Address, AddressDto>();

        // 3) User -> UserDto demonstrates:
        //    - custom member resolver (FullName)
        //    - flattening (AddressStreet)
        //    - ignoring sensitive fields (Password)
        //    - mapping collections (Contacts)
        //    - custom conversion from AgeString -> Age via resolver
        cfg.CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, src => string.Join(' ', new[] { src.FirstName, src.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))))
            .ForMember(dest => dest.AddressStreet, src => (src.Address is null ? null : $"{src.Address.Street}, {src.Address.City}")! )
            .ForMember(dest => dest.Contacts, src => src.Contacts) // let the mapper map individual Contact -> ContactDto items
            .ForMember(dest => dest.Id, src => src.Id + 1000) // demonstrate mapping with transform
            .ForMember(dest => dest.Age, src => {
                if (int.TryParse(src.AgeString, out var a)) return a; return 0; })
            .Ignore(dest => dest.Password);

        // 4) Order -> OrderDto using a full-type converter (ConvertUsing)
        cfg.CreateMap<Order, OrderDto>()
            .ConvertUsing(o => new OrderDto
            {
                Id = o.Id,
                TotalFormatted = o.Total.ToString("C"),
                Status = o.Status.ToString()
            });

        var mapper = cfg.BuildMapper();

        // Create a complex user
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Address = new Address { Street = "Main St", City = "Metropolis" },
            Password = "super-secret",
            AgeString = "42",
            Contacts = new List<Contact>
            {
                new("Email", "john.doe@example.com"),
                new("Phone", "+1-555-1234")
            }
        };

        // Map single object
        var dto = mapper.Map<User, UserDto>(user);

        Console.WriteLine("-- Single User mapping --");
        Console.WriteLine($"Id (transformed): {dto.Id}");
        Console.WriteLine($"FullName: {dto.FullName}");
        Console.WriteLine($"AddressStreet: {dto.AddressStreet}");
        Console.WriteLine($"Password (ignored): {dto.Password ?? "<null>"}");
        Console.WriteLine($"Age (parsed): {dto.Age}");
        Console.WriteLine("Contacts:");
        foreach (var c in dto.Contacts)
            Console.WriteLine($"  - {c.Type}: {c.Value}");

        // Map a collection of users
        var users = new List<User> 
        { 
            user, new() { Id = 2, FirstName = "Jane", LastName = "Smith", 
            Contacts = [new Contact("Email", "jane@x.com")], AgeString = "27" } 
        };
        
        // Use the object-based Map overload for collections and cast the result to List<UserDto>
        var userDtos = (List<UserDto>)mapper.Map(users, users.GetType(), typeof(List<UserDto>));

        Console.WriteLine("\n-- Collection mapping --");
        Console.WriteLine($"Mapped {userDtos.Count} users");

        // Map using ConvertUsing example
        var order = new Order { Id = 100, Total = 123.45m, Status = OrderStatus.Completed };
        var orderDto = mapper.Map<Order, OrderDto>(order);

        Console.WriteLine("\n-- ConvertUsing mapping (Order -> OrderDto) --");
        Console.WriteLine($"Order Id: {orderDto.Id}, Total: {orderDto.TotalFormatted}, Status: {orderDto.Status}");

        // Demonstrate low-level Map(object, Type, Type)
        var boxed = mapper.Map(user, typeof(User), typeof(UserDto));
        Console.WriteLine("\n-- Map(object, Type, Type) result type: " + boxed.GetType().Name);

        Console.WriteLine("\nFinished sample run.");
    }
}
