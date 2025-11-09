namespace Fast_Mapper.Sample.Models.Source;

public class User
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Address? Address { get; set; }
    public string? Password { get; set; }
    public IEnumerable<Contact> Contacts { get; set; } = Enumerable.Empty<Contact>();
    public string? AgeString { get; set; } // demonstrates primitive conversion/resolver
}