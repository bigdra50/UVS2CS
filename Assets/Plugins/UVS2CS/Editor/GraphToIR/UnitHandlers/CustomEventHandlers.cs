using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class CustomEventHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is TriggerCustomEvent or CustomEvent or BoltUnityEvent;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            if (unit is TriggerCustomEvent trigger)
            {
                var eventName = resolver.Resolve(trigger.valueInputs["name"]);
                var target = trigger.valueInputs.Contains("target")
                    ? resolver.Resolve(trigger.valueInputs["target"])
                    : new IRThis();

                var call = new IRMethodCall
                {
                    MethodName = "Trigger",
                    DeclaringType = new IRTypeRef
                    {
                        FullName = "Unity.VisualScripting.CustomEvent",
                        ShortName = "CustomEvent",
                        Namespace = "Unity.VisualScripting",
                    },
                    IsStatic = true,
                };
                call.Arguments.Add(eventName);
                call.Arguments.Add(target);

                return new IRExpressionStatement { Expression = call };
            }
            return null;
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            if (unit is CustomEvent customEvent)
            {
                return new IRIdentifier { Name = port.key };
            }
            return new IRNull();
        }
    }
}
