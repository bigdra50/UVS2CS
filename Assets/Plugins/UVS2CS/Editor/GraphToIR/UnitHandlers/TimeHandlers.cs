using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class TimeHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is Timer or Cooldown or WaitUnit;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            switch (unit)
            {
                case WaitForSecondsUnit wait:
                {
                    var seconds = resolver.Resolve(wait.valueInputs["seconds"]);
                    return new IRYieldReturn
                    {
                        Expression = new IRConstructorCall
                        {
                            Type = new IRTypeRef { FullName = "UnityEngine.WaitForSeconds", ShortName = "WaitForSeconds", Namespace = "UnityEngine" },
                            Arguments = { seconds },
                        },
                    };
                }
                case WaitForNextFrameUnit:
                    return new IRYieldReturn { Expression = new IRNull() };
                case WaitForEndOfFrameUnit:
                    return new IRYieldReturn
                    {
                        Expression = new IRConstructorCall
                        {
                            Type = new IRTypeRef { FullName = "UnityEngine.WaitForEndOfFrame", ShortName = "WaitForEndOfFrame", Namespace = "UnityEngine" },
                        },
                    };
                case WaitUntilUnit waitUntil:
                    return new IRYieldReturn
                    {
                        Expression = new IRConstructorCall
                        {
                            Type = new IRTypeRef { FullName = "UnityEngine.WaitUntil", ShortName = "WaitUntil", Namespace = "UnityEngine" },
                        },
                    };
                case WaitWhileUnit:
                    return new IRYieldReturn
                    {
                        Expression = new IRConstructorCall
                        {
                            Type = new IRTypeRef { FullName = "UnityEngine.WaitWhile", ShortName = "WaitWhile", Namespace = "UnityEngine" },
                        },
                    };
                case Timer:
                case Cooldown:
                    return new IRExpressionStatement
                    {
                        Expression = new IRIdentifier { Name = $"/* {unit.GetType().Name}: complex timer - requires manual conversion */" },
                    };
                default:
                    return null;
            }
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            if (unit is Timer timer)
            {
                return port.key switch
                {
                    "elapsedSeconds" => new IRIdentifier { Name = "elapsedSeconds" },
                    "elapsedRatio" => new IRIdentifier { Name = "elapsedRatio" },
                    "remainingSeconds" => new IRIdentifier { Name = "remainingSeconds" },
                    "remainingRatio" => new IRIdentifier { Name = "remainingRatio" },
                    _ => new IRNull(),
                };
            }
            return new IRNull();
        }
    }
}
