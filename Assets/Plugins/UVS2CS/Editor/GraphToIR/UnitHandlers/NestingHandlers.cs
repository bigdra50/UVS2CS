using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class NestingHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is GraphInput or GraphOutput or SubgraphUnit;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            switch (unit)
            {
                case GraphOutput:
                    return new IRReturn { Value = null };
                case SubgraphUnit:
                    return new IRExpressionStatement
                    {
                        Expression = new IRIdentifier { Name = $"/* SubGraph: {unit.GetType().Name} - inline expansion needed */" },
                    };
                default:
                    return null;
            }
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case GraphInput:
                    return new IRIdentifier { Name = port.key };
                default:
                    return new IRNull();
            }
        }
    }
}
