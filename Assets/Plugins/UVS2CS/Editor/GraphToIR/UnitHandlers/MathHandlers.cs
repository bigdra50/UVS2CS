using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;
using BinOp = UVS2CS.IR.BinaryOperator;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class MathHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit)
        {
            var typeName = unit.GetType().Name;
            if (typeName.Contains("Add") || typeName.Contains("Subtract")
                || typeName.Contains("Multiply") || typeName.Contains("Divide")
                || typeName.Contains("Modulo"))
            {
                return unit.valueInputs.Any(p => p.key == "a")
                    && unit.valueInputs.Any(p => p.key == "b");
            }
            return false;
        }

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            var typeName = unit.GetType().Name;

            if (typeName.Contains("Add")) return MathBinary(unit, BinOp.Add, resolver);
            if (typeName.Contains("Subtract")) return MathBinary(unit, BinOp.Subtract, resolver);
            if (typeName.Contains("Multiply")) return MathBinary(unit, BinOp.Multiply, resolver);
            if (typeName.Contains("Divide")) return MathBinary(unit, BinOp.Divide, resolver);
            if (typeName.Contains("Modulo")) return MathBinary(unit, BinOp.Modulo, resolver);

            return new IRNull();
        }

        static IRBinaryOp MathBinary(IUnit unit, BinOp op, ValueResolver resolver)
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
