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

        FlowGraph _graph;
        AssetJsonReader _jsonFallback;
        Dictionary<string, IUnit> _unitByGuid;

        public List<IRStatement> PreambleStatements { get; } = new();

        public void SetJsonFallback(AssetJsonReader json)
        {
            _jsonFallback = json;
        }

        public ValueResolver(UnitHandlerRegistry registry)
        {
            _registry = registry;
        }

        public void AnalyzeFanOut(FlowGraph graph)
        {
            _graph = graph;
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

        /// <summary>
        /// Define() 失敗でポートが存在しない Unit の値入力を、グラフの接続コレクションから解決する。
        /// </summary>
        public IRExpression ResolveByKey(IUnit unit, string portKey)
        {
            if (_graph == null) return ResolveDefaultByKey(unit, portKey);

            // ポートが存在する場合は通常解決
            var port = unit.valueInputs.FirstOrDefault(p => p.key == portKey);
            if (port != null) return Resolve(port);

            // 正常な接続コレクションから探す
            foreach (var conn in _graph.valueConnections)
            {
                if (!conn.destinationExists || !conn.sourceExists) continue;
                if (conn.destination.unit != unit || conn.destination.key != portKey) continue;

                var sourceUnit = conn.source.unit;
                var sourcePort = conn.source;
                return ResolveFromUnit(sourceUnit, sourcePort);
            }

            // InvalidConnection からリフレクション経由で接続元を探す（Define失敗した接続）
            foreach (var conn in _graph.invalidConnections)
            {
                if (!ConnectionResolver.TryGetDestInfo(conn, out var destUnit, out var destKey))
                    continue;
                if (destUnit != unit || destKey != portKey)
                    continue;
                if (!ConnectionResolver.TryGetSourceInfo(conn, out var srcUnit, out var srcKey))
                    continue;

                var handler = _registry.GetHandler(srcUnit);
                if (handler != null)
                {
                    // ポートが存在する場合はポートオブジェクトを渡す
                    // Define() 失敗でポートが空の場合は null を渡す（handler 側で対応）
                    var srcValueOutput = srcUnit.valueOutputs.FirstOrDefault(p => p.key == srcKey);
                    return handler.HandleValue(srcUnit, srcValueOutput, this);
                }

                // handler がない場合でも、srcUnit が MemberUnit なら member フィールドから読む
                if (srcUnit is Unity.VisualScripting.MemberUnit memberUnit && memberUnit.member != null)
                {
                    if (srcKey == "value" || srcKey == "result")
                    {
                        var target = ResolveByKey(srcUnit, "target");
                        return new IRMemberAccess
                        {
                            Target = target ?? new IRThis(),
                            MemberName = memberUnit.member.name,
                        };
                    }
                }
            }

            return ResolveDefaultByKey(unit, portKey);
        }

        /// <summary>
        /// Define() 失敗した Unit のデフォルト値を取得する。
        /// </summary>
        IRExpression ResolveDefaultByKey(IUnit unit, string portKey)
        {
            if (unit.defaultValues.TryGetValue(portKey, out var value))
            {
                return new IRLiteral
                {
                    Value = value,
                    Type = IRTypeRef.FromType(value?.GetType()),
                };
            }
            return new IRNull();
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
