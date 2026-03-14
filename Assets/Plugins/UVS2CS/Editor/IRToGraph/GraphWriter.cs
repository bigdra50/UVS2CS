using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UVS2CS.IR;

namespace UVS2CS.IRToGraph
{
    public sealed class GraphWriter
    {
        public FlowGraph Write(IRGraph ir)
        {
            var graph = new FlowGraph();
            var layout = new LayoutCalculator();
            var factory = new UnitFactory(graph, layout);
            var conn = new ConnectionBuilder(graph);

            WriteVariables(ir, graph);

            foreach (var method in ir.Methods)
            {
                WriteMethod(method, graph, factory, conn, layout);
                layout.NextMethod();
            }

            return graph;
        }

        public ScriptGraphAsset WriteAsset(IRGraph ir)
        {
            var asset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
            var graph = Write(ir);
            // ScriptGraphAsset.graph is from Macro<FlowGraph>, set via reflection or property
            var graphProp = typeof(ScriptGraphAsset).BaseType?
                .GetProperty("graph", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            graphProp?.SetValue(asset, graph);
            asset.name = ir.ClassName;
            return asset;
        }

        static void WriteVariables(IRGraph ir, FlowGraph graph)
        {
            foreach (var field in ir.Fields.Where(f => f.Origin == VariableOrigin.Graph))
            {
                var value = (field.DefaultValue as IRLiteral)?.Value;
                graph.variables.Set(field.Name, value);
            }
        }

        void WriteMethod(IRMethod method, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout)
        {
            var eventUnit = factory.CreateLifecycleEvent(method.Name);
            var triggerPort = GetTriggerPort(eventUnit);

            if (triggerPort == null || method.Body == null) return;

            ControlOutput lastOutput = triggerPort;
            WriteBlock(method.Body, graph, factory, conn, layout, ref lastOutput);
        }

        void WriteBlock(IRBlock block, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            foreach (var stmt in block.Statements)
                WriteStatement(stmt, graph, factory, conn, layout, ref lastOutput);
        }

        void WriteStatement(IRStatement stmt, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            switch (stmt)
            {
                case IRExpressionStatement exprStmt:
                    WriteExpressionStatement(exprStmt, factory, conn, ref lastOutput);
                    break;

                case IRAssignment assign:
                    WriteAssignment(assign, factory, conn, ref lastOutput);
                    break;

                case IRIf ifStmt:
                    WriteIf(ifStmt, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRFor forStmt:
                    WriteFor(forStmt, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRForEach forEach:
                    WriteForEach(forEach, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRWhile whileStmt:
                    WriteWhile(whileStmt, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRBreak:
                    var breakUnit = factory.CreateBreak();
                    conn.ConnectControl(lastOutput, breakUnit.controlInputs.First());
                    lastOutput = null;
                    break;

                case IRBlock block:
                    WriteBlock(block, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRSwitch switchStmt:
                    WriteSwitchAsIfChain(switchStmt, graph, factory, conn, layout, ref lastOutput);
                    break;

                case IRYieldReturn yieldRet:
                    // WaitForSeconds 等のコルーチン yield → コメントとして残す
                    break;

                case IRVariableDeclaration decl:
                    if (decl.Initializer != null)
                    {
                        WriteAssignment(new IRAssignment
                        {
                            Target = new IRIdentifier { Name = decl.Name },
                            Value = decl.Initializer,
                        }, factory, conn, ref lastOutput);
                    }
                    break;

                case IRTryCatch:
                case IRThrow:
                case IRReturn:
                    // 複雑な制御フロー → Graph では直接表現が困難
                    break;
            }
        }

        void WriteExpressionStatement(IRExpressionStatement exprStmt, UnitFactory factory,
            ConnectionBuilder conn, ref ControlOutput lastOutput)
        {
            if (exprStmt.Expression is IRMethodCall call)
            {
                var type = call.DeclaringType?.ResolvedType ?? ResolveType(call.DeclaringType);
                if (type == null) return;

                var paramTypes = call.Arguments
                    .Select(_ => typeof(object))
                    .ToArray();

                var invoke = factory.CreateInvokeMember(type, call.MethodName, paramTypes);

                if (invoke.controlInputs.Contains("enter"))
                {
                    conn.ConnectControl(lastOutput, invoke.controlInputs["enter"]);
                    lastOutput = invoke.controlOutputs.Contains("exit")
                        ? invoke.controlOutputs["exit"]
                        : null;
                }

                ConnectArguments(call, invoke, factory, conn);
            }
        }

        void WriteAssignment(IRAssignment assign, UnitFactory factory,
            ConnectionBuilder conn, ref ControlOutput lastOutput)
        {
            if (assign.Target is IRIdentifier id)
            {
                var setVar = factory.CreateSetVariable(id.Name, VariableKind.Graph);

                conn.ConnectControl(lastOutput, setVar.controlInputs["assign"]);

                var valueUnit = CreateValueUnit(assign.Value, factory);
                if (valueUnit != null)
                    conn.ConnectValue(valueUnit.valueOutputs.First(), setVar.valueInputs["input"]);

                var nameUnit = factory.CreateLiteral(id.Name, typeof(string));
                conn.ConnectValue(nameUnit.valueOutputs["output"], setVar.valueInputs["name"]);

                lastOutput = setVar.controlOutputs.Contains("assigned")
                    ? setVar.controlOutputs["assigned"]
                    : null;
            }
        }

        void WriteIf(IRIf ifStmt, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            var ifUnit = factory.CreateIf();

            conn.ConnectControl(lastOutput, ifUnit.controlInputs["enter"]);

            var condUnit = CreateValueUnit(ifStmt.Condition, factory);
            if (condUnit != null)
                conn.ConnectValue(condUnit.valueOutputs.First(), ifUnit.valueInputs["condition"]);

            if (ifStmt.ThenBody != null)
            {
                layout.NewRow();
                ControlOutput thenOutput = ifUnit.controlOutputs["ifTrue"];
                WriteBlock(ifStmt.ThenBody, graph, factory, conn, layout, ref thenOutput);
            }

            if (ifStmt.ElseBody != null)
            {
                layout.NewRow();
                ControlOutput elseOutput = ifUnit.controlOutputs["ifFalse"];
                WriteBlock(ifStmt.ElseBody, graph, factory, conn, layout, ref elseOutput);
            }

            lastOutput = null;
        }

        void WriteSwitchAsIfChain(IRSwitch switchStmt, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            // Switch を If チェーンとして展開
            foreach (var section in switchStmt.Sections)
            {
                var condition = new IRBinaryOp
                {
                    Left = switchStmt.Value,
                    Right = section.Label,
                    Operator = IR.BinaryOperator.Equal,
                };
                var ifStmt = new IRIf
                {
                    Condition = condition,
                    ThenBody = section.Body,
                };
                WriteIf(ifStmt, graph, factory, conn, layout, ref lastOutput);
            }

            if (switchStmt.DefaultBody != null)
                WriteBlock(switchStmt.DefaultBody, graph, factory, conn, layout, ref lastOutput);
        }

        void WriteFor(IRFor forStmt, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            var forUnit = factory.CreateFor();

            conn.ConnectControl(lastOutput, forUnit.controlInputs["enter"]);

            var firstUnit = CreateValueUnit(forStmt.First, factory);
            if (firstUnit != null)
                conn.ConnectValue(firstUnit.valueOutputs.First(), forUnit.valueInputs["firstIndex"]);

            var lastUnit = CreateValueUnit(forStmt.Last, factory);
            if (lastUnit != null)
                conn.ConnectValue(lastUnit.valueOutputs.First(), forUnit.valueInputs["lastIndex"]);

            var stepUnit = CreateValueUnit(forStmt.Step, factory);
            if (stepUnit != null)
                conn.ConnectValue(stepUnit.valueOutputs.First(), forUnit.valueInputs["step"]);

            if (forStmt.Body != null)
            {
                layout.NewRow();
                ControlOutput bodyOutput = forUnit.controlOutputs["body"];
                WriteBlock(forStmt.Body, graph, factory, conn, layout, ref bodyOutput);
            }

            lastOutput = forUnit.controlOutputs.Contains("exit")
                ? forUnit.controlOutputs["exit"]
                : null;
        }

        void WriteForEach(IRForEach forEach, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            var forEachUnit = factory.CreateForEach();

            conn.ConnectControl(lastOutput, forEachUnit.controlInputs["enter"]);

            var collUnit = CreateValueUnit(forEach.Collection, factory);
            if (collUnit != null)
                conn.ConnectValue(collUnit.valueOutputs.First(), forEachUnit.valueInputs["collection"]);

            if (forEach.Body != null)
            {
                layout.NewRow();
                ControlOutput bodyOutput = forEachUnit.controlOutputs["body"];
                WriteBlock(forEach.Body, graph, factory, conn, layout, ref bodyOutput);
            }

            lastOutput = forEachUnit.controlOutputs.Contains("exit")
                ? forEachUnit.controlOutputs["exit"]
                : null;
        }

        void WriteWhile(IRWhile whileStmt, FlowGraph graph, UnitFactory factory,
            ConnectionBuilder conn, LayoutCalculator layout, ref ControlOutput lastOutput)
        {
            var whileUnit = factory.CreateWhile();

            conn.ConnectControl(lastOutput, whileUnit.controlInputs["enter"]);

            var condUnit = CreateValueUnit(whileStmt.Condition, factory);
            if (condUnit != null)
                conn.ConnectValue(condUnit.valueOutputs.First(), whileUnit.valueInputs["condition"]);

            if (whileStmt.Body != null)
            {
                layout.NewRow();
                ControlOutput bodyOutput = whileUnit.controlOutputs["body"];
                WriteBlock(whileStmt.Body, graph, factory, conn, layout, ref bodyOutput);
            }

            lastOutput = whileUnit.controlOutputs.Contains("exit")
                ? whileUnit.controlOutputs["exit"]
                : null;
        }

        Unit CreateValueUnit(IRExpression expr, UnitFactory factory)
        {
            switch (expr)
            {
                case IRLiteral lit:
                    var type = lit.Type?.ResolvedType ?? lit.Value?.GetType() ?? typeof(object);
                    return factory.CreateLiteral(lit.Value, type);

                case IRIdentifier id:
                    return factory.CreateGetVariable(id.Name, VariableKind.Graph);

                case IRBinaryOp bin:
                    // For simple cases, use default values on the unit
                    // Complex binary ops would need GenericAdd etc.
                    return null;

                case IRNull:
                    return factory.CreateLiteral(null, typeof(object));

                default:
                    return null;
            }
        }

        void ConnectArguments(IRMethodCall call, InvokeMember invoke, UnitFactory factory,
            ConnectionBuilder conn)
        {
            var paramInputs = invoke.valueInputs.Where(p => p.key.StartsWith("%")).ToList();

            for (var i = 0; i < call.Arguments.Count && i < paramInputs.Count; i++)
            {
                var argUnit = CreateValueUnit(call.Arguments[i], factory);
                if (argUnit != null)
                    conn.ConnectValue(argUnit.valueOutputs.First(), paramInputs[i]);
            }
        }

        static ControlOutput GetTriggerPort(IUnit unit)
        {
            return unit.controlOutputs.FirstOrDefault(p => p.key == "trigger");
        }

        static Type ResolveType(IRTypeRef typeRef)
        {
            if (typeRef?.ResolvedType != null) return typeRef.ResolvedType;
            if (typeRef?.FullName == null) return null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                var type = asm.GetType(typeRef.FullName);
                if (type != null) return type;
            }

            return null;
        }
    }
}
