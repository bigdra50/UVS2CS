using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class LiteralHandler : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is Literal or This or Null or Formula;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            return unit switch
            {
                Literal literal => new IRLiteral
                {
                    Value = literal.value,
                    Type = IRTypeRef.FromType(literal.type),
                },
                This => new IRThis(),
                Null => new IRNull(),
                Formula formula => new IRIdentifier { Name = $"/* Formula: requires manual conversion */" },
                _ => new IRNull(),
            };
        }
    }
}
