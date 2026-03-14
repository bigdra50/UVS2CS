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

        public FlowTracer(UnitHandlerRegistry registry, ValueResolver resolver)
        {
            _registry = registry;
            _resolver = resolver;
        }

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

        static ControlOutput GetContinuationPort(IUnit unit, ControlInput entryPort)
        {
            switch (unit)
            {
                // 分岐系: ハンドラ内で各分岐を TraceFrom するため継続なし
                case If:
                case SwitchOnInteger:
                case SwitchOnString:
                case SwitchOnEnum:
                case TryCatch:
                case ToggleFlow:
                    return null;

                // ループ系: exit ポートが継続
                case While:
                case For:
                case ForEach:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");

                // Once: after ポートが継続（once は初回のみ）
                case Once:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "after");

                // SelectOnFlow: exit ポートが継続
                case SelectOnFlow:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");

                // WaitUnit 系: exit ポートが継続（コルーチン完了後）
                case WaitUnit:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");

                // Timer/Cooldown: 複数出力がある複雑な Unit、継続なし
                case Timer:
                case Cooldown:
                    return null;

                // デフォルト: exit → assigned → 最初のポート
                default:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit")
                        ?? unit.controlOutputs.FirstOrDefault(p => p.key == "assigned")
                        ?? unit.controlOutputs.FirstOrDefault();
            }
        }
    }
}
