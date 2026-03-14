using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class ControlFlowHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is If or Sequence or For or ForEach or While or Break
            or TryCatch or Throw
            or SwitchOnInteger or SwitchOnString or SwitchOnEnum
            or Cache or Once or ToggleFlow or ToggleValue
            or SelectOnFlow or SelectOnInteger or SelectOnString or SelectOnEnum;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            return unit switch
            {
                If ifUnit => HandleIf(ifUnit, tracer, resolver),
                Sequence seq => HandleSequence(seq, tracer, resolver),
                For forUnit => HandleFor(forUnit, tracer, resolver),
                ForEach forEach => HandleForEach(forEach, tracer, resolver),
                While whileUnit => HandleWhile(whileUnit, tracer, resolver),
                Break => new IRBreak(),
                TryCatch tryCatch => HandleTryCatch(tryCatch, tracer, resolver),
                Throw throwUnit => HandleThrow(throwUnit, resolver),
                SwitchOnInteger sw => HandleSwitch(sw, tracer, resolver),
                SwitchOnString sw => HandleSwitch(sw, tracer, resolver),
                SwitchOnEnum sw => HandleSwitchEnum(sw, tracer, resolver),
                Cache cache => HandleCache(cache, tracer, resolver),
                Once once => HandleOnce(once, tracer, resolver),
                ToggleFlow toggle => HandleToggleFlow(toggle, tracer, resolver),
                _ => null,
            };
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case Cache cache:
                    return resolver.Resolve(cache.valueInputs["input"]);
                case ToggleValue toggle:
                    return new IRConditional
                    {
                        Condition = resolver.Resolve(toggle.valueInputs["condition"]),
                        WhenTrue = resolver.Resolve(toggle.valueInputs["onValue"]),
                        WhenFalse = resolver.Resolve(toggle.valueInputs["offValue"]),
                    };
                case SelectOnFlow:
                    return new IRIdentifier { Name = "/* SelectOnFlow result */" };
                case SelectOnInteger:
                case SelectOnString:
                case SelectOnEnum:
                {
                    var selector = resolver.Resolve(unit.valueInputs["selector"]);
                    return new IRIdentifier { Name = $"/* Select({ExpressionName(selector)}) */" };
                }
                default:
                    return null;
            }
        }

        IRStatement HandleIf(If ifUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var condition = resolver.Resolve(ifUnit.valueInputs["condition"]);
            var thenPort = ifUnit.controlOutputs["ifTrue"];
            var elsePort = ifUnit.controlOutputs["ifFalse"];
            var thenBlock = tracer.TraceFrom(thenPort);
            var elseBlock = tracer.TraceFrom(elsePort);

            return new IRIf
            {
                Condition = condition,
                ThenBody = thenBlock,
                ElseBody = elseBlock.Statements.Count > 0 ? elseBlock : null,
            };
        }

        IRStatement HandleSequence(Sequence seq, FlowTracer tracer, ValueResolver resolver)
        {
            var block = new IRBlock();
            foreach (var output in seq.controlOutputs)
            {
                var subBlock = tracer.TraceFrom(output);
                foreach (var stmt in subBlock.Statements)
                    block.Statements.Add(stmt);
            }
            return block;
        }

        IRStatement HandleFor(For forUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var first = resolver.Resolve(forUnit.valueInputs["firstIndex"]);
            var last = resolver.Resolve(forUnit.valueInputs["lastIndex"]);
            var step = resolver.Resolve(forUnit.valueInputs["step"]);
            var bodyPort = forUnit.controlOutputs["body"];
            var body = tracer.TraceFrom(bodyPort);

            return new IRFor
            {
                IndexVariable = "i",
                First = first,
                Last = last,
                Step = step,
                Body = body,
            };
        }

        IRStatement HandleForEach(ForEach forEach, FlowTracer tracer, ValueResolver resolver)
        {
            var collection = resolver.Resolve(forEach.valueInputs["collection"]);
            var bodyPort = forEach.controlOutputs["body"];
            var body = tracer.TraceFrom(bodyPort);

            return new IRForEach
            {
                ItemVariable = "item",
                Collection = collection,
                Body = body,
            };
        }

        IRStatement HandleWhile(While whileUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var condition = resolver.Resolve(whileUnit.valueInputs["condition"]);
            var bodyPort = whileUnit.controlOutputs["body"];
            var body = tracer.TraceFrom(bodyPort);

            return new IRWhile
            {
                Condition = condition,
                Body = body,
            };
        }

        IRStatement HandleTryCatch(TryCatch tryCatchUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var tryPort = tryCatchUnit.controlOutputs.FirstOrDefault(p => p.key == "try");
            var catchPort = tryCatchUnit.controlOutputs.FirstOrDefault(p => p.key == "catch");
            var finallyPort = tryCatchUnit.controlOutputs.FirstOrDefault(p => p.key == "finally");

            return new IR.IRTryCatch
            {
                TryBody = tryPort != null ? tracer.TraceFrom(tryPort) : new IRBlock(),
                ExceptionType = new IRTypeRef { FullName = "System.Exception", ShortName = "Exception" },
                ExceptionVariable = "ex",
                CatchBody = catchPort != null ? tracer.TraceFrom(catchPort) : null,
                FinallyBody = finallyPort != null ? tracer.TraceFrom(finallyPort) : null,
            };
        }

        IRStatement HandleThrow(Throw throwUnit, ValueResolver resolver)
        {
            var exInput = throwUnit.valueInputs.FirstOrDefault(p => p.key == "exception");
            return new IRThrow
            {
                Expression = exInput != null ? resolver.Resolve(exInput) : null,
            };
        }

        IRStatement HandleSwitch<T>(SwitchUnit<T> switchUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var value = resolver.Resolve(switchUnit.valueInputs["selector"]);
            var irSwitch = new IRSwitch { Value = value };

            foreach (var branch in switchUnit.branches)
            {
                var body = tracer.TraceFrom(branch.Value);
                irSwitch.Sections.Add(new IRSwitchSection
                {
                    Label = new IRLiteral { Value = branch.Key, Type = IRTypeRef.FromType(typeof(T)) },
                    Body = body,
                });
            }

            var defaultPort = switchUnit.controlOutputs.FirstOrDefault(p => p.key == "default");
            if (defaultPort != null)
                irSwitch.DefaultBody = tracer.TraceFrom(defaultPort);

            return irSwitch;
        }

        IRStatement HandleSwitchEnum(SwitchOnEnum switchUnit, FlowTracer tracer, ValueResolver resolver)
        {
            var value = resolver.Resolve(switchUnit.valueInputs["selector"]);
            var irSwitch = new IRSwitch { Value = value };

            foreach (var output in switchUnit.controlOutputs)
            {
                if (output.key == "default") continue;
                var body = tracer.TraceFrom(output);
                irSwitch.Sections.Add(new IRSwitchSection
                {
                    Label = new IRIdentifier { Name = output.key },
                    Body = body,
                });
            }

            var defaultPort = switchUnit.controlOutputs.FirstOrDefault(p => p.key == "default");
            if (defaultPort != null)
                irSwitch.DefaultBody = tracer.TraceFrom(defaultPort);

            return irSwitch;
        }

        IRStatement HandleCache(Cache cache, FlowTracer tracer, ValueResolver resolver)
        {
            var input = resolver.Resolve(cache.valueInputs["input"]);
            return new IRVariableDeclaration
            {
                Name = "_cached",
                Initializer = input,
            };
        }

        IRStatement HandleOnce(Once once, FlowTracer tracer, ValueResolver resolver)
        {
            var oncePort = once.controlOutputs["once"];
            var afterPort = once.controlOutputs["after"];
            var onceBlock = tracer.TraceFrom(oncePort);

            return new IRIf
            {
                Condition = new IRUnaryOp
                {
                    Operand = new IRIdentifier { Name = "_onceDone" },
                    Operator = IR.UnaryOperator.LogicalNot,
                },
                ThenBody = new IRBlock
                {
                    Statements =
                    {
                        new IRAssignment
                        {
                            Target = new IRIdentifier { Name = "_onceDone" },
                            Value = new IRLiteral { Value = true, Type = IRTypeRef.Bool },
                        },
                        onceBlock,
                    },
                },
            };
        }

        IRStatement HandleToggleFlow(ToggleFlow toggle, FlowTracer tracer, ValueResolver resolver)
        {
            var onPort = toggle.controlOutputs.FirstOrDefault(p => p.key == "turnedOn");
            var offPort = toggle.controlOutputs.FirstOrDefault(p => p.key == "turnedOff");

            var onBlock = onPort != null ? tracer.TraceFrom(onPort) : new IRBlock();
            var offBlock = offPort != null ? tracer.TraceFrom(offPort) : new IRBlock();

            return new IRIf
            {
                Condition = new IRIdentifier { Name = "_isToggleOn" },
                ThenBody = onBlock,
                ElseBody = offBlock.Statements.Count > 0 ? offBlock : null,
            };
        }

        static string ExpressionName(IRExpression expr) => expr is IRIdentifier id ? id.Name : "value";
    }
}
