using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class MemberHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) => unit is InvokeMember or GetMember or SetMember or CreateStruct or Expose;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            if (unit is InvokeMember invoke)
            {
                var call = BuildMethodCall(invoke, resolver);
                return new IRExpressionStatement { Expression = call };
            }

            if (unit is SetMember setMember)
            {
                var target = ResolveTarget(setMember, resolver);
                var value = resolver.Resolve(setMember.valueInputs["input"]);

                return new IRAssignment
                {
                    Target = new IRMemberAccess
                    {
                        Target = target,
                        MemberName = setMember.member.name,
                    },
                    Value = value,
                };
            }

            return null;
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case InvokeMember invoke:
                    return BuildMethodCall(invoke, resolver);

                case GetMember getMember:
                {
                    var target = ResolveTarget(getMember, resolver);
                    return new IRMemberAccess
                    {
                        Target = target,
                        MemberName = getMember.member.name,
                    };
                }

                case SetMember setMember:
                {
                    var nameExpr = resolver.Resolve(setMember.valueInputs["input"]);
                    return nameExpr;
                }

                case CreateStruct createStruct:
                {
                    var ctor = new IRConstructorCall
                    {
                        Type = IRTypeRef.FromType(createStruct.type),
                    };
                    return ctor;
                }

                case Expose expose:
                {
                    var input = resolver.Resolve(expose.valueInputs.First());
                    return new IRMemberAccess
                    {
                        Target = input,
                        MemberName = port.key,
                    };
                }

                default:
                    return new IRNull();
            }
        }

        IRMethodCall BuildMethodCall(InvokeMember invoke, ValueResolver resolver)
        {
            var member = invoke.member;
            var call = new IRMethodCall
            {
                MethodName = member.name,
                DeclaringType = IRTypeRef.FromType(member.targetType),
                IsStatic = !member.requiresTarget,
            };

            if (member.requiresTarget)
            {
                var targetInput = invoke.valueInputs.FirstOrDefault(p => p.key == "target");
                if (targetInput != null)
                    call.Target = resolver.Resolve(targetInput);
            }

            foreach (var input in invoke.valueInputs)
            {
                if (input.key == "target") continue;
                if (!input.key.StartsWith("%")) continue;

                call.Arguments.Add(resolver.Resolve(input));
            }

            return call;
        }

        IRExpression ResolveTarget(MemberUnit memberUnit, ValueResolver resolver)
        {
            if (!memberUnit.member.requiresTarget)
                return null;

            var targetInput = memberUnit.valueInputs.FirstOrDefault(p => p.key == "target");
            if (targetInput != null)
                return resolver.Resolve(targetInput);

            return new IRThis();
        }
    }
}
