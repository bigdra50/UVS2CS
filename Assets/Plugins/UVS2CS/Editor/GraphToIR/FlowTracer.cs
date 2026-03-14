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
                case If:
                    return null;
                case While:
                case For:
                case ForEach:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit");
                default:
                    return unit.controlOutputs.FirstOrDefault(p => p.key == "exit")
                        ?? unit.controlOutputs.FirstOrDefault();
            }
        }
    }
}
