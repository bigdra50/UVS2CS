using System.Linq;
using Unity.VisualScripting;
using UVS2CS.GraphToIR.UnitHandlers;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR
{
    public sealed class FlowTracer
    {
        readonly UnitHandlerRegistry _registry;
        readonly ValueResolver _resolver;
        FlowGraph _graph;

        public FlowTracer(UnitHandlerRegistry registry, ValueResolver resolver)
        {
            _registry = registry;
            _resolver = resolver;
        }

        public void SetGraph(FlowGraph graph) => _graph = graph;

        public IRBlock TraceFrom(ControlOutput startPort)
        {
            var block = new IRBlock();
            var currentPort = startPort;

            while (currentPort != null && currentPort.hasValidConnection)
            {
                var conn = currentPort.connection;
                if (conn == null) break;

                var destInput = conn.destination;
                var unit = destInput.unit;

                var handler = _registry.GetHandler(unit);
                if (handler == null)
                {
                    block.Statements.Add(new IRExpressionStatement
                    {
                        Expression = new IRIdentifier { Name = $"/* unhandled: {unit.GetType().Name} */" },
                    });
                    currentPort = GetContinuationPort(unit, destInput);
                    continue;
                }

                var stmt = handler.HandleControlFlow(unit, this, _resolver);
                if (stmt != null)
                    block.Statements.Add(stmt);

                currentPort = GetContinuationPort(unit, destInput);
            }

            return block;
        }

        ControlOutput GetContinuationPort(IUnit unit, ControlInput entryPort)
        {
            // Define() 失敗で controlOutputs が空の場合、グラフの接続コレクションから探す
            if (unit.controlOutputs.Count == 0 && _graph != null)
                return FindContinuationFromGraph(unit, entryPort?.key ?? "enter", "exit");

            switch (unit)
            {
                case If:
                case SwitchOnInteger:
                case SwitchOnString:
                case SwitchOnEnum:
                case TryCatch:
                case ToggleFlow:
                    return null;

                case While:
                case For:
                case ForEach:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");

                case Once:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "after");

                case SelectOnFlow:
                case WaitUnit:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");

                case Timer:
                case Cooldown:
                    return null;

                default:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit")
                        ?? unit.controlOutputs.FirstOrDefault(p => p.key == "assigned")
                        ?? unit.controlOutputs.FirstOrDefault();
            }
        }

        /// <summary>
        /// Define() 失敗した Unit の制御フロー継続を、グラフの接続コレクションから直接探す。
        /// </summary>
        ControlOutput FindContinuationFromGraph(IUnit unit, string entryKey, string exitKey)
        {
            // 正常な controlConnections から探す
            foreach (var conn in _graph.controlConnections)
            {
                if (!conn.sourceExists) continue;
                if (conn.source.unit != unit) continue;
                if (conn.source.key == exitKey)
                    return conn.source;
            }

            foreach (var conn in _graph.controlConnections)
            {
                if (!conn.sourceExists) continue;
                if (conn.source.unit != unit) continue;
                return conn.source;
            }

            // invalidConnections からリフレクションで探す
            foreach (var conn in _graph.invalidConnections)
            {
                if (!ConnectionResolver.TryGetSourceInfo(conn, out var srcUnit, out var srcKey))
                    continue;
                if (srcUnit != unit) continue;
                if (srcKey == exitKey)
                {
                    var port = unit.controlOutputs.FirstOrDefault(p => p.key == exitKey);
                    return port;
                }
            }

            return null;
        }
    }
}
