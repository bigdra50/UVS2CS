using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class NullHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is NullCheck or NullCoalesce;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case NullCheck nullCheck:
                {
                    var input = resolver.Resolve(nullCheck.valueInputs["input"]);
                    return port.key == "notNull"
                        ? new IRNullCheck { Value = input, IsNull = false }
                        : new IRNullCheck { Value = input, IsNull = true };
                }
                case NullCoalesce nullCoalesce:
                {
                    var input = resolver.Resolve(nullCoalesce.valueInputs["input"]);
                    var fallback = resolver.Resolve(nullCoalesce.valueInputs["fallback"]);
                    return new IRNullCoalesce { Left = input, Fallback = fallback };
                }
                default:
                    return new IRNull();
            }
        }
    }
}
