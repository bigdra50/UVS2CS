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
                var value = resolver.ResolveByKey(setMember, "input");

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
                    return resolver.ResolveByKey(setMember, "input");

                case CreateStruct createStruct:
                    return new IRConstructorCall
                    {
                        Type = IRTypeRef.FromType(createStruct.type),
                    };

                case Expose expose:
                {
                    var firstInput = expose.valueInputs.FirstOrDefault();
                    var input = firstInput != null
                        ? resolver.Resolve(firstInput)
                        : new IRThis();
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
                call.Target = resolver.ResolveByKey(invoke, "target");

            // ポートが存在する場合: ポート経由で引数を解決
            var paramInputs = invoke.valueInputs.Where(p => p.key.StartsWith("%")).ToList();
            if (paramInputs.Count > 0)
            {
                foreach (var input in paramInputs)
                    call.Arguments.Add(resolver.Resolve(input));
            }
            else
            {
                // Define() 失敗でポートがない場合: defaultValues から引数名を推定
                foreach (var kv in invoke.defaultValues)
                {
                    if (!kv.Key.StartsWith("%")) continue;
                    call.Arguments.Add(resolver.ResolveByKey(invoke, kv.Key));
                }
            }

            return call;
        }

        IRExpression ResolveTarget(MemberUnit memberUnit, ValueResolver resolver)
        {
            if (!memberUnit.member.requiresTarget)
                return null;

            return resolver.ResolveByKey(memberUnit, "target");
        }
    }
}
