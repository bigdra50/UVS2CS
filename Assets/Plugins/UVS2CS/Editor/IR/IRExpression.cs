using System.Collections.Generic;

namespace UVS2CS.IR
{
    public abstract class IRExpression { }

    public sealed class IRLiteral : IRExpression
    {
        public object Value { get; set; }
        public IRTypeRef Type { get; set; }
    }

    public sealed class IRIdentifier : IRExpression
    {
        public string Name { get; set; }
    }

    public sealed class IRThis : IRExpression { }

    public sealed class IRNull : IRExpression { }

    public sealed class IRMemberAccess : IRExpression
    {
        public IRExpression Target { get; set; }
        public string MemberName { get; set; }
    }

    public sealed class IRMethodCall : IRExpression
    {
        public IRExpression Target { get; set; }
        public string MethodName { get; set; }
        public IRTypeRef DeclaringType { get; set; }
        public List<IRExpression> Arguments { get; } = new();
        public bool IsStatic { get; set; }
    }

    public sealed class IRConstructorCall : IRExpression
    {
        public IRTypeRef Type { get; set; }
        public List<IRExpression> Arguments { get; } = new();
    }

    public sealed class IRBinaryOp : IRExpression
    {
        public IRExpression Left { get; set; }
        public IRExpression Right { get; set; }
        public BinaryOperator Operator { get; set; }
    }

    public sealed class IRUnaryOp : IRExpression
    {
        public IRExpression Operand { get; set; }
        public UnaryOperator Operator { get; set; }
    }

    public sealed class IRCast : IRExpression
    {
        public IRExpression Operand { get; set; }
        public IRTypeRef TargetType { get; set; }
    }

    public sealed class IRNullCheck : IRExpression
    {
        public IRExpression Value { get; set; }
        public bool IsNull { get; set; }
    }

    public sealed class IRNullCoalesce : IRExpression
    {
        public IRExpression Left { get; set; }
        public IRExpression Fallback { get; set; }
    }

    public sealed class IRConditional : IRExpression
    {
        public IRExpression Condition { get; set; }
        public IRExpression WhenTrue { get; set; }
        public IRExpression WhenFalse { get; set; }
    }

    public sealed class IRIndexAccess : IRExpression
    {
        public IRExpression Target { get; set; }
        public IRExpression Index { get; set; }
    }

    public enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        And,
        Or,
        Xor,
        Equal,
        NotEqual,
        Greater,
        GreaterOrEqual,
        Less,
        LessOrEqual,
    }

    public enum UnaryOperator
    {
        Negate,
        LogicalNot,
    }
}
