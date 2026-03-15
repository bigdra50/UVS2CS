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
            TraceChain(startPort, block);
            return block;
        }

        void TraceChain(ControlOutput startPort, IRBlock block)
        {
            var currentPort = startPort;

            while (currentPort != null && currentPort.hasValidConnection)
            {
                var conn = currentPort.connection;
                if (conn == null) break;

                var destInput = conn.destination;
                var unit = destInput.unit;

                ProcessUnit(unit, destInput, block);

                currentPort = GetContinuationPort(unit, destInput);
            }

            // currentPort が null になった場合（Define失敗で継続ポートが取れない）
            // invalidConnections から次の Unit を探して続行
            if (currentPort == null && block.Statements.Count > 0 && _graph != null)
                TryContinueViaInvalidConnections(block);
        }

        /// <summary>
        /// Unit を処理して IR ステートメントを生成する。
        /// Define() 失敗した Unit も ConnectionResolver 経由で処理する。
        /// </summary>
        void ProcessUnit(IUnit unit, ControlInput entryPort, IRBlock block)
        {
            var handler = _registry.GetHandler(unit);
            if (handler == null)
            {
                block.Statements.Add(new IRExpressionStatement
                {
                    Expression = new IRIdentifier { Name = $"/* unhandled: {unit.GetType().Name} */" },
                });
                return;
            }

            var stmt = handler.HandleControlFlow(unit, this, _resolver);
            if (stmt != null)
                block.Statements.Add(stmt);
        }

        /// <summary>
        /// invalidConnections を走査し、最後に処理した Unit からの制御フロー継続を探す。
        /// </summary>
        void TryContinueViaInvalidConnections(IRBlock block)
        {
            // invalidConnections から、処理済み Unit の exit ポートから次の Unit への接続を探す
            foreach (var conn in _graph.invalidConnections)
            {
                if (!ConnectionResolver.TryGetSourceInfo(conn, out var srcUnit, out var srcKey))
                    continue;
                if (!ConnectionResolver.TryGetDestInfo(conn, out var destUnit, out var destKey))
                    continue;

                // exit / assigned ポートからの接続のみ
                if (srcKey != "exit" && srcKey != "assigned") continue;

                var handler = _registry.GetHandler(destUnit);
                if (handler != null)
                {
                    var stmt = handler.HandleControlFlow(destUnit, this, _resolver);
                    if (stmt != null)
                        block.Statements.Add(stmt);
                }
            }
        }

        ControlOutput GetContinuationPort(IUnit unit, ControlInput entryPort)
        {
            // Define() 失敗で controlOutputs が空
            if (unit.controlOutputs.Count == 0)
                return null; // TryContinueViaInvalidConnections で後続処理

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
    }
}
