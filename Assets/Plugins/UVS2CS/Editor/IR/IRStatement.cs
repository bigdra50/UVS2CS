using System.Collections.Generic;

namespace UVS2CS.IR
{
    public abstract class IRStatement { }

    public sealed class IRBlock : IRStatement
    {
        public List<IRStatement> Statements { get; } = new();
    }

    public sealed class IRExpressionStatement : IRStatement
    {
        public IRExpression Expression { get; set; }
    }

    public sealed class IRAssignment : IRStatement
    {
        public IRExpression Target { get; set; }
        public IRExpression Value { get; set; }
    }

    public sealed class IRVariableDeclaration : IRStatement
    {
        public string Name { get; set; }
        public IRTypeRef Type { get; set; }
        public IRExpression Initializer { get; set; }
    }

    public sealed class IRIf : IRStatement
    {
        public IRExpression Condition { get; set; }
        public IRBlock ThenBody { get; set; }
        public IRBlock ElseBody { get; set; }
    }

    public sealed class IRFor : IRStatement
    {
        public string IndexVariable { get; set; }
        public IRExpression First { get; set; }
        public IRExpression Last { get; set; }
        public IRExpression Step { get; set; }
        public IRBlock Body { get; set; }
    }

    public sealed class IRForEach : IRStatement
    {
        public string ItemVariable { get; set; }
        public IRTypeRef ItemType { get; set; }
        public IRExpression Collection { get; set; }
        public IRBlock Body { get; set; }
    }

    public sealed class IRWhile : IRStatement
    {
        public IRExpression Condition { get; set; }
        public IRBlock Body { get; set; }
    }

    public sealed class IRReturn : IRStatement
    {
        public IRExpression Value { get; set; }
    }

    public sealed class IRBreak : IRStatement { }

    public sealed class IRSwitch : IRStatement
    {
        public IRExpression Value { get; set; }
        public List<IRSwitchSection> Sections { get; } = new();
        public IRBlock DefaultBody { get; set; }
    }

    public sealed class IRSwitchSection
    {
        public IRExpression Label { get; set; }
        public IRBlock Body { get; set; }
    }

    public sealed class IRYieldReturn : IRStatement
    {
        public IRExpression Expression { get; set; }
    }

    public sealed class IRThrow : IRStatement
    {
        public IRExpression Expression { get; set; }
    }

    public sealed class IRTryCatch : IRStatement
    {
        public IRBlock TryBody { get; set; }
        public IRTypeRef ExceptionType { get; set; }
        public string ExceptionVariable { get; set; }
        public IRBlock CatchBody { get; set; }
        public IRBlock FinallyBody { get; set; }
    }
}
