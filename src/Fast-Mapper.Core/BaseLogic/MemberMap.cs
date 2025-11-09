using Fast_Mapper.Core.Abstractions;

namespace Fast_Mapper.Core.BaseLogic;

/// <summary>
/// 
/// </summary>
internal class MemberMap : IMemberMap
{
    public string DestinationMemberName { get; set; } = null!;
    public bool Ignored { get; set; }
    public Func<object, object?>? Resolver { get; set; }
    public object? Resolve(object source) => Resolver?.Invoke(source);
}