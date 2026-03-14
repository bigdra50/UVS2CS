using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public interface IUnitHandler
    {
        bool CanHandle(IUnit unit);
        IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver);
        IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver);
    }
}
