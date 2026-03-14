using System.Collections.Generic;

namespace UVS2CS.IR
{
    public sealed class IRGraph
    {
        public string ClassName { get; set; }
        public string Namespace { get; set; }
        public List<IRUsing> Usings { get; } = new();
        public List<IRField> Fields { get; } = new();
        public List<IRMethod> Methods { get; } = new();
    }

    public sealed class IRUsing
    {
        public string Namespace { get; set; }
    }

    public sealed class IRField
    {
        public string Name { get; set; }
        public IRTypeRef Type { get; set; }
        public IRExpression DefaultValue { get; set; }
        public FieldModifier Modifier { get; set; }
        public VariableOrigin Origin { get; set; }
    }

    public enum FieldModifier
    {
        Private,
        SerializeField,
        Public,
    }

    public enum VariableOrigin
    {
        Graph,
        Object,
        Scene,
        Application,
        Saved,
        Flow,
        Local,
    }

    public sealed class IRMethod
    {
        public string Name { get; set; }
        public MethodKind Kind { get; set; }
        public IRTypeRef ReturnType { get; set; }
        public List<IRParameter> Parameters { get; } = new();
        public IRBlock Body { get; set; }
        public AccessModifier Access { get; set; }
    }

    public enum MethodKind
    {
        Lifecycle,
        Custom,
        Coroutine,
    }

    public enum AccessModifier
    {
        Private,
        Protected,
        Public,
    }

    public sealed class IRParameter
    {
        public string Name { get; set; }
        public IRTypeRef Type { get; set; }
    }
}
