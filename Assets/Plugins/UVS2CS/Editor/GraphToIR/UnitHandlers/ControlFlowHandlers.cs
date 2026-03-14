using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class ControlFlowHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is If or Sequence or For or ForEach or While or Break;

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
                _ => null,
            };
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver) => null;

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
    }
}
