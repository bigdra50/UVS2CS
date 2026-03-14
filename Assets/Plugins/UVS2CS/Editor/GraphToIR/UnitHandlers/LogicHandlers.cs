using Unity.VisualScripting;
using UVS2CS.IR;
using BinOp = UVS2CS.IR.BinaryOperator;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class LogicHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is And or Or or Negate or ExclusiveOr
            or Equal or NotEqual
            or Greater or GreaterOrEqual or Less or LessOrEqual
            or Comparison or EqualityComparison or NumericComparison
            or ApproximatelyEqual or NotApproximatelyEqual;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case Negate neg:
                    return new IRUnaryOp
                    {
                        Operand = resolver.Resolve(neg.valueInputs["input"]),
                        Operator = IR.UnaryOperator.LogicalNot,
                    };

                case And:
                    return BinaryFromUnit(unit, BinOp.And, resolver);
                case Or:
                    return BinaryFromUnit(unit, BinOp.Or, resolver);
                case ExclusiveOr:
                    return BinaryFromUnit(unit, BinOp.Xor, resolver);
                case Equal:
                    return BinaryFromUnit(unit, BinOp.Equal, resolver);
                case NotEqual:
                    return BinaryFromUnit(unit, BinOp.NotEqual, resolver);
                case Greater:
                    return BinaryFromUnit(unit, BinOp.Greater, resolver);
                case GreaterOrEqual:
                    return BinaryFromUnit(unit, BinOp.GreaterOrEqual, resolver);
                case Less:
                    return BinaryFromUnit(unit, BinOp.Less, resolver);
                case LessOrEqual:
                    return BinaryFromUnit(unit, BinOp.LessOrEqual, resolver);

                case ApproximatelyEqual:
                {
                    var a = resolver.Resolve(unit.valueInputs["a"]);
                    var b = resolver.Resolve(unit.valueInputs["b"]);
                    return new IRMethodCall
                    {
                        MethodName = "Approximately",
                        DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" },
                        IsStatic = true,
                        Arguments = { a, b },
                    };
                }
                case NotApproximatelyEqual:
                {
                    var a = resolver.Resolve(unit.valueInputs["a"]);
                    var b = resolver.Resolve(unit.valueInputs["b"]);
                    return new IRUnaryOp
                    {
                        Operand = new IRMethodCall
                        {
                            MethodName = "Approximately",
                            DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" },
                            IsStatic = true,
                            Arguments = { a, b },
                        },
                        Operator = IR.UnaryOperator.LogicalNot,
                    };
                }
                case Comparison:
                case EqualityComparison:
                case NumericComparison:
                    return BinaryFromUnit(unit, BinOp.Equal, resolver);

                default:
                    return new IRNull();
            }
        }

        static IRBinaryOp BinaryFromUnit(IUnit unit, BinOp op, ValueResolver resolver)
        {
            return new IRBinaryOp
            {
                Left = resolver.Resolve(unit.valueInputs["a"]),
                Right = resolver.Resolve(unit.valueInputs["b"]),
                Operator = op,
            };
        }
    }
}
