using System;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.IRToGraph
{
    public sealed class UnitFactory
    {
        readonly FlowGraph _graph;
        readonly LayoutCalculator _layout;

        public UnitFactory(FlowGraph graph, LayoutCalculator layout)
        {
            _graph = graph;
            _layout = layout;
        }

        public Unit AddUnit(Unit unit)
        {
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public Start CreateStart()
        {
            var unit = new Start { position = _layout.EventPosition() };
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public Update CreateUpdate()
        {
            var unit = new Update { position = _layout.EventPosition() };
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public Unit CreateLifecycleEvent(string methodName)
        {
            Unit unit = methodName switch
            {
                "Start" => new Start(),
                "Update" => new Update(),
                "FixedUpdate" => new FixedUpdate(),
                "LateUpdate" => new LateUpdate(),
                "OnEnable" => new OnEnable(),
                "OnDisable" => new OnDisable(),
                "OnDestroy" => new OnDestroy(),
                _ => new Start(),
            };

            unit.position = _layout.EventPosition();
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public If CreateIf()
        {
            var unit = new If();
            return (If)AddUnit(unit);
        }

        public For CreateFor()
        {
            var unit = new For();
            return (For)AddUnit(unit);
        }

        public ForEach CreateForEach()
        {
            var unit = new ForEach();
            return (ForEach)AddUnit(unit);
        }

        public While CreateWhile()
        {
            var unit = new While();
            return (While)AddUnit(unit);
        }

        public Literal CreateLiteral(object value, Type type)
        {
            var unit = new Literal(type, value);
            return (Literal)AddUnit(unit);
        }

        public GetVariable CreateGetVariable(string name, VariableKind kind)
        {
            var unit = new GetVariable { defaultValues = { ["name"] = name } };
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public SetVariable CreateSetVariable(string name, VariableKind kind)
        {
            var unit = new SetVariable { defaultValues = { ["name"] = name } };
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            unit.Define();
            return unit;
        }

        public InvokeMember CreateInvokeMember(Type targetType, string methodName, Type[] paramTypes)
        {
            var member = new Member(targetType, methodName, paramTypes);
            var unit = new InvokeMember(member);
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            try { unit.Define(); }
            catch { /* member reflection may fail in test context */ }
            return unit;
        }

        public GetMember CreateGetMember(Type targetType, string memberName)
        {
            var member = new Member(targetType, memberName);
            var unit = new GetMember(member);
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            try { unit.Define(); }
            catch { }
            return unit;
        }

        public SetMember CreateSetMember(Type targetType, string memberName)
        {
            var member = new Member(targetType, memberName);
            var unit = new SetMember(member);
            unit.position = _layout.Next();
            _graph.units.Add(unit);
            try { unit.Define(); }
            catch { }
            return unit;
        }

        public Break CreateBreak()
        {
            var unit = new Break();
            return (Break)AddUnit(unit);
        }
    }
}
