using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class EventHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is IEventUnit;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver) => null;

        public static string GetMethodName(IUnit unit)
        {
            return unit switch
            {
                Start => "Start",
                Update => "Update",
                FixedUpdate => "FixedUpdate",
                LateUpdate => "LateUpdate",
                OnEnable => "OnEnable",
                OnDisable => "OnDisable",
                OnDestroy => "OnDestroy",
                _ => unit.GetType().Name,
            };
        }

        public static ControlOutput GetTriggerPort(IUnit unit)
        {
            foreach (var port in unit.controlOutputs)
            {
                if (port.key == "trigger")
                    return port;
            }
            return null;
        }
    }
}
