using System.Collections.Generic;

namespace UVS2CS.GraphToIR.Serialized
{
    public sealed class StructValue
    {
        public string TypeName { get; set; }
        public Dictionary<string, object> Fields { get; } = new();
    }

    /// <summary>
    /// .asset の _data._json から構築されるグラフの完全なスナップショット。
    /// Unity のデシリアライズに依存しない、port key ベースのデータモデル。
    /// </summary>
    public sealed class SerializedGraphSnapshot
    {
        public Dictionary<string, SerializedUnit> Units { get; } = new();
        public List<SerializedEdge> ControlEdges { get; } = new();
        public List<SerializedEdge> ValueEdges { get; } = new();
        public Dictionary<string, object> Variables { get; } = new();
    }

    public sealed class SerializedUnit
    {
        public string Id { get; set; }
        public string TypeName { get; set; }
        public UnitKind Kind { get; set; }
        public SerializedMember Member { get; set; }
        public Dictionary<string, object> DefaultValues { get; } = new();
        public float PositionX { get; set; }
        public float PositionY { get; set; }

        // VariableKind (GetVariable/SetVariable)
        public string VariableKind { get; set; }

        // CustomEvent
        public int ArgumentCount { get; set; }

        // Sequence
        public int OutputCount { get; set; }

        // Literal
        public object LiteralValue { get; set; }
        public string LiteralType { get; set; }

        // Coroutine flag
        public bool Coroutine { get; set; }
    }

    public sealed class SerializedMember
    {
        public string Name { get; set; }
        public string TargetTypeName { get; set; }
        public List<string> ParameterTypeNames { get; } = new();
        public bool RequiresTarget { get; set; } = true;
    }

    public sealed class SerializedEdge
    {
        public string SourceUnitId { get; set; }
        public string SourceKey { get; set; }
        public string DestUnitId { get; set; }
        public string DestKey { get; set; }
    }

    /// <summary>
    /// port key ベースのポート参照。Unit の Define() 状態に依存しない。
    /// </summary>
    public readonly struct PortRef
    {
        public readonly string UnitId;
        public readonly string Key;

        public PortRef(string unitId, string key)
        {
            UnitId = unitId;
            Key = key;
        }

        public override string ToString() => $"{UnitId}:{Key}";
    }

    public enum UnitKind
    {
        Unknown,
        Event,
        ControlFlow,
        Variable,
        InvokeMember,
        GetMember,
        SetMember,
        Literal,
        Math,
        Logic,
        Comparison,
        Null,
        NullCheck,
        Collection,
        Time,
        Nesting,
        CustomEvent,
        TriggerCustomEvent,
        CreateStruct,
        Expose,
        Formula,
    }
}
