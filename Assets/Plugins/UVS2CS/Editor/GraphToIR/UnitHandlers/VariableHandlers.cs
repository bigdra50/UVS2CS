using Unity.VisualScripting;
using UVS2CS.IR;
using BinOp = UVS2CS.IR.BinaryOperator;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class VariableHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is GetVariable or SetVariable or IsVariableDefined;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            if (unit is SetVariable setVar)
            {
                var nameExpr = resolver.Resolve(setVar.valueInputs["name"]);
                var valueExpr = resolver.Resolve(setVar.valueInputs["input"]);
                var varName = ExtractVariableName(nameExpr);

                return new IRAssignment
                {
                    Target = new IRIdentifier { Name = varName },
                    Value = valueExpr,
                };
            }

            return null;
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case GetVariable getVar:
                {
                    var nameExpr = resolver.Resolve(getVar.valueInputs["name"]);
                    var varName = ExtractVariableName(nameExpr);
                    return new IRIdentifier { Name = varName };
                }
                case SetVariable setVar:
                {
                    var nameExpr = resolver.Resolve(setVar.valueInputs["name"]);
                    var varName = ExtractVariableName(nameExpr);
                    return new IRIdentifier { Name = varName };
                }
                case IsVariableDefined isDefined:
                {
                    var nameExpr = resolver.Resolve(isDefined.valueInputs["name"]);
                    var varName = ExtractVariableName(nameExpr);
                    return new IRBinaryOp
                    {
                        Left = new IRIdentifier { Name = varName },
                        Right = new IRNull(),
                        Operator = BinOp.NotEqual,
                    };
                }
                default:
                    return new IRNull();
            }
        }

        static string ExtractVariableName(IRExpression nameExpr)
        {
            if (nameExpr is IRLiteral { Value: string name })
                return name;
            return "unknown";
        }
    }
}
