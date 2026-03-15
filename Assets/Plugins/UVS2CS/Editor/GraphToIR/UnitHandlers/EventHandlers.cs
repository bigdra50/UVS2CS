using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class EventHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is IEventUnit;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            // Physics events expose collision/trigger data
            return new IRIdentifier { Name = port.key };
        }

        public static string GetMethodName(IUnit unit)
        {
            return unit switch
            {
                // Lifecycle
                Start => "Start",
                Update => "Update",
                FixedUpdate => "FixedUpdate",
                LateUpdate => "LateUpdate",
                OnEnable => "OnEnable",
                OnDisable => "OnDisable",
                OnDestroy => "OnDestroy",

                // Physics 3D
                OnCollisionEnter => "OnCollisionEnter",
                OnCollisionStay => "OnCollisionStay",
                OnCollisionExit => "OnCollisionExit",
                OnTriggerEnter => "OnTriggerEnter",
                OnTriggerStay => "OnTriggerStay",
                OnTriggerExit => "OnTriggerExit",

                // Physics 2D
                OnCollisionEnter2D => "OnCollisionEnter2D",
                OnCollisionStay2D => "OnCollisionStay2D",
                OnCollisionExit2D => "OnCollisionExit2D",
                OnTriggerEnter2D => "OnTriggerEnter2D",
                OnTriggerStay2D => "OnTriggerStay2D",
                OnTriggerExit2D => "OnTriggerExit2D",

                // Mouse
                OnMouseDown => "OnMouseDown",
                OnMouseUp => "OnMouseUp",
                OnMouseDrag => "OnMouseDrag",
                OnMouseEnter => "OnMouseEnter",
                OnMouseExit => "OnMouseExit",
                OnMouseOver => "OnMouseOver",
                OnMouseUpAsButton => "OnMouseUpAsButton",

                // Application
                OnApplicationFocus => "OnApplicationFocus",
                OnApplicationPause => "OnApplicationPause",
                OnApplicationQuit => "OnApplicationQuit",

                // Rendering
                OnBecameVisible => "OnBecameVisible",
                OnBecameInvisible => "OnBecameInvisible",

                // Animation
                OnAnimatorMove => "OnAnimatorMove",
                OnAnimatorIK => "OnAnimatorIK",

                // GUI
                OnGUI => "OnGUI",

                // Hierarchy
                OnTransformParentChanged => "OnTransformParentChanged",
                OnTransformChildrenChanged => "OnTransformChildrenChanged",

                // CustomEvent: defaultValues["name"] からイベント名をメソッド名にする
                CustomEvent ce => ExtractCustomEventName(ce),

                // Fallback: use type name
                _ => unit.GetType().Name,
            };
        }

        static string ExtractCustomEventName(CustomEvent ce)
        {
            if (ce.defaultValues.TryGetValue("name", out var name) && name is string s && !string.IsNullOrEmpty(s))
                return "On" + s.Replace(" ", "");
            return "OnCustomEvent";
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
