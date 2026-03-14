using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UVS2CS.GraphToIR.UnitHandlers;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR
{
    public sealed class ValueResolver
    {
        readonly UnitHandlerRegistry _registry;
        readonly Dictionary<ValueOutput, int> _fanOutCounts = new();
        readonly Dictionary<ValueOutput, string> _tempVarNames = new();
        int _tempVarCounter;

        public List<IRStatement> PreambleStatements { get; } = new();

        public ValueResolver(UnitHandlerRegistry registry)
        {
            _registry = registry;
        }

        public void AnalyzeFanOut(FlowGraph graph)
        {
            _fanOutCounts.Clear();
            foreach (var conn in graph.valueConnections)
            {
                if (!conn.sourceExists) continue;
                var source = conn.source;
                _fanOutCounts.TryGetValue(source, out var count);
                _fanOutCounts[source] = count + 1;
            }
        }

        public IRExpression Resolve(ValueInput port)
        {
            if (port == null) return new IRNull();

            if (port.hasValidConnection)
            {
                var conn = port.connection;
                if (conn == null) return ResolveDefault(port);

                var sourcePort = conn.source;
                var sourceUnit = sourcePort.unit;

                if (_fanOutCounts.TryGetValue(sourcePort, out var count) && count > 1)
                {
                    if (_tempVarNames.TryGetValue(sourcePort, out var existingName))
                        return new IRIdentifier { Name = existingName };

                    var expr = ResolveFromUnit(sourceUnit, sourcePort);
                    var tempName = $"_temp{_tempVarCounter++}";
                    _tempVarNames[sourcePort] = tempName;

                    PreambleStatements.Add(new IRVariableDeclaration
                    {
                        Name = tempName,
                        Initializer = expr,
                    });

                    return new IRIdentifier { Name = tempName };
                }

                return ResolveFromUnit(sourceUnit, sourcePort);
            }

            return ResolveDefault(port);
        }

        IRExpression ResolveFromUnit(IUnit unit, ValueOutput port)
        {
            var handler = _registry.GetHandler(unit);
            if (handler != null)
                return handler.HandleValue(unit, port, this);

            return new IRIdentifier { Name = $"/* unhandled: {unit.GetType().Name} */" };
        }

        IRExpression ResolveDefault(ValueInput port)
        {
            if (port.unit.defaultValues.TryGetValue(port.key, out var value))
            {
                return new IRLiteral
                {
                    Value = value,
                    Type = IRTypeRef.FromType(value?.GetType() ?? port.type),
                };
            }

            if (port.type == typeof(string)) return new IRLiteral { Value = "", Type = IRTypeRef.String };
            if (port.type == typeof(bool)) return new IRLiteral { Value = false, Type = IRTypeRef.Bool };
            if (port.type == typeof(int)) return new IRLiteral { Value = 0, Type = IRTypeRef.Int };
            if (port.type == typeof(float)) return new IRLiteral { Value = 0f, Type = IRTypeRef.Float };

            return new IRNull();
        }
    }
}
