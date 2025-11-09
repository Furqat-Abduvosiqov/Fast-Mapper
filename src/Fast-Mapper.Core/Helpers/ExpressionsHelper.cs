using System.Linq.Expressions;
using Fast_Mapper.Core.Exceptions;

namespace Fast_Mapper.Core.Helpers;

/// <summary>
/// Expressions helper class
/// </summary>
internal static class ExpressionsHelper
{
    public static string GetMemberName<T, TMember>(Expression<Func<T, TMember>> expr)
    {
        if (expr.Body is MemberExpression member)
            return member.Member.Name;

        // handle conversions: dest => (object)dest.Prop
        if (expr.Body is UnaryExpression { Operand: MemberExpression member2 })
            return member2.Member.Name;

        throw new MapperException("Expression must be a member access");
    }
}