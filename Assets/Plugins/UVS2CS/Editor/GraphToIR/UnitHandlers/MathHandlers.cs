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
            // ポートが存在する場合はポートキーで判定、Define失敗時は型名のみで判定
            var hasPorts = unit.valueInputs.Count > 0;
            return IsBinaryMath(typeName) && (!hasPorts || HasPorts(unit, "a", "b"))
                || IsUnaryMath(typeName) && (!hasPorts || HasPorts(unit, "input"))
                || IsMultiInputMath(typeName)
                || typeName.Contains("Lerp")
                || typeName.Contains("MoveTowards")
                || typeName.Contains("Exponentiate");
        }

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver) => null;

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            var typeName = unit.GetType().Name;

            if (IsBinaryMath(typeName))
            {
                if (typeName.Contains("Add")) return MathBinary(unit, BinOp.Add, resolver);
                if (typeName.Contains("Subtract")) return MathBinary(unit, BinOp.Subtract, resolver);
                if (typeName.Contains("Multiply")) return MathBinary(unit, BinOp.Multiply, resolver);
                if (typeName.Contains("Divide")) return MathBinary(unit, BinOp.Divide, resolver);
                if (typeName.Contains("Modulo")) return MathBinary(unit, BinOp.Modulo, resolver);
                if (typeName.Contains("Minimum")) return MathfCall("Min", unit, resolver);
                if (typeName.Contains("Maximum")) return MathfCall("Max", unit, resolver);
                if (typeName.Contains("DotProduct")) return StaticCall("Vector3", "Dot", unit, resolver);
                if (typeName.Contains("CrossProduct")) return StaticCall("Vector3", "Cross", unit, resolver);
                if (typeName.Contains("Distance")) return StaticCall("Vector3", "Distance", unit, resolver);
                if (typeName.Contains("Angle")) return StaticCall("Vector3", "Angle", unit, resolver);
                if (typeName.Contains("Project")) return StaticCall("Vector3", "Project", unit, resolver);
            }

            if (IsUnaryMath(typeName))
            {
                var input = resolver.ResolveByKey(unit, "input");
                if (typeName.Contains("Absolute")) return MathfCall1("Abs", input);
                if (typeName.Contains("Normalize")) return new IRMemberAccess { Target = input, MemberName = "normalized" };
                if (typeName.Contains("Round")) return MathfCall1("Round", input);
                if (typeName.Contains("Root")) return MathfCall1("Sqrt", input);
                if (typeName.Contains("PerSecond"))
                    return new IRBinaryOp
                    {
                        Left = input,
                        Right = new IRMemberAccess { Target = new IRIdentifier { Name = "Time" }, MemberName = "deltaTime" },
                        Operator = BinOp.Multiply,
                    };
            }

            if (typeName.Contains("Lerp"))
            {
                var t = resolver.ResolveByKey(unit, "t");
                return new IRMethodCall
                {
                    MethodName = "Lerp",
                    DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" },
                    IsStatic = true,
                    Arguments = { resolver.Resolve(unit.valueInputs["a"]), resolver.Resolve(unit.valueInputs["b"]), t },
                };
            }

            if (typeName.Contains("MoveTowards"))
            {
                return new IRMethodCall
                {
                    MethodName = "MoveTowards",
                    DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" },
                    IsStatic = true,
                    Arguments = { resolver.ResolveByKey(unit, "current"), resolver.ResolveByKey(unit, "target"), resolver.ResolveByKey(unit, "maxDelta") },
                };
            }

            if (typeName.Contains("Exponentiate"))
            {
                return new IRMethodCall
                {
                    MethodName = "Pow",
                    DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" },
                    IsStatic = true,
                    Arguments = { resolver.ResolveByKey(unit, "base"), resolver.ResolveByKey(unit, "exponent") },
                };
            }

            if (typeName.Contains("Sum") || typeName.Contains("Average"))
            {
                var inputs = unit.valueInputs.Where(p => p.key != "count").ToList();
                if (inputs.Count == 0) return new IRLiteral { Value = 0, Type = IRTypeRef.Int };

                IRExpression result = resolver.Resolve(inputs[0]);
                for (var i = 1; i < inputs.Count; i++)
                    result = new IRBinaryOp { Left = result, Right = resolver.Resolve(inputs[i]), Operator = BinOp.Add };

                if (typeName.Contains("Average"))
                    result = new IRBinaryOp { Left = result, Right = new IRLiteral { Value = inputs.Count, Type = IRTypeRef.Int }, Operator = BinOp.Divide };

                return result;
            }

            return new IRNull();
        }

        static bool IsBinaryMath(string n) =>
            n.Contains("Add") || n.Contains("Subtract") || n.Contains("Multiply")
            || n.Contains("Divide") || n.Contains("Modulo")
            || n.Contains("Minimum") || n.Contains("Maximum")
            || n.Contains("DotProduct") || n.Contains("CrossProduct")
            || n.Contains("Distance") || n.Contains("Angle") || n.Contains("Project");

        static bool IsUnaryMath(string n) =>
            n.Contains("Absolute") || n.Contains("Normalize") || n.Contains("Round")
            || n.Contains("Root") || n.Contains("PerSecond");

        static bool IsMultiInputMath(string n) => n.Contains("Sum") || n.Contains("Average");

        static bool HasPorts(IUnit unit, params string[] keys) =>
            keys.All(k => unit.valueInputs.Any(p => p.key == k));

        static IRBinaryOp MathBinary(IUnit unit, BinOp op, ValueResolver resolver) =>
            new() { Left = resolver.ResolveByKey(unit, "a"), Right = resolver.ResolveByKey(unit, "b"), Operator = op };

        static IRMethodCall MathfCall(string method, IUnit unit, ValueResolver resolver) =>
            new() { MethodName = method, DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" }, IsStatic = true, Arguments = { resolver.ResolveByKey(unit, "a"), resolver.ResolveByKey(unit, "b") } };

        static IRMethodCall MathfCall1(string method, IRExpression input) =>
            new() { MethodName = method, DeclaringType = new IRTypeRef { FullName = "UnityEngine.Mathf", ShortName = "Mathf" }, IsStatic = true, Arguments = { input } };

        static IRMethodCall StaticCall(string typeName, string method, IUnit unit, ValueResolver resolver) =>
            new() { MethodName = method, DeclaringType = new IRTypeRef { ShortName = typeName, FullName = $"UnityEngine.{typeName}" }, IsStatic = true, Arguments = { resolver.ResolveByKey(unit, "a"), resolver.ResolveByKey(unit, "b") } };
    }
}
