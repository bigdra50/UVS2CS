using System;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UVS2CS.GraphToIR;
using UVS2CS.IR;
using UVS2CS.IRToCSharp;

namespace UVS2CS.Tests.GraphToIR
{
    public class GraphReaderTests
    {
        static FlowGraph CreateGraph()
        {
            return new FlowGraph();
        }

        static void AddAndDefine(FlowGraph graph, Unit unit, Vector2 position = default)
        {
            unit.position = position;
            graph.units.Add(unit);
            try { unit.Define(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Define() failed for {unit.GetType().Name}: {e.Message}");
            }
        }

        static void ConnectControl(FlowGraph graph, ControlOutput source, ControlInput dest)
        {
            graph.controlConnections.Add(new ControlConnection(source, dest));
        }

        static void ConnectValue(FlowGraph graph, ValueOutput source, ValueInput dest)
        {
            graph.valueConnections.Add(new ValueConnection(source, dest));
        }

        [Test]
        public void Read_EmptyGraph_ReturnsGraphWithClassName()
        {
            var graph = CreateGraph();
            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");

            Assert.AreEqual("TestScript", ir.ClassName);
            Assert.AreEqual(0, ir.Methods.Count);
        }

        [Test]
        public void Read_StartWithSetVariable_GeneratesAssignment()
        {
            var graph = CreateGraph();

            var start = new Start();
            AddAndDefine(graph, start);

            var setVar = new SetVariable();
            AddAndDefine(graph, setVar);

            ConnectControl(graph, start.controlOutputs["trigger"], setVar.controlInputs["assign"]);

            var nameLiteral = new Literal(typeof(string), "score");
            AddAndDefine(graph, nameLiteral);
            ConnectValue(graph, nameLiteral.valueOutputs["output"], setVar.valueInputs["name"]);

            var valueLiteral = new Literal(typeof(int), 100);
            AddAndDefine(graph, valueLiteral);
            ConnectValue(graph, valueLiteral.valueOutputs["output"], setVar.valueInputs["input"]);

            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");

            Assert.AreEqual(1, ir.Methods.Count);
            Assert.AreEqual("Start", ir.Methods[0].Name);
            Assert.IsTrue(ir.Methods[0].Body.Statements.Count > 0);

            var code = CSharpEmitter.Emit(ir);
            StringAssert.Contains("score = 100", code);
        }

        [Test]
        public void Read_IfBranch_GeneratesIfStatement()
        {
            var graph = CreateGraph();

            var start = new Start();
            AddAndDefine(graph, start);

            var ifUnit = new If();
            AddAndDefine(graph, ifUnit);

            ConnectControl(graph, start.controlOutputs["trigger"], ifUnit.controlInputs["enter"]);

            var condLiteral = new Literal(typeof(bool), true);
            AddAndDefine(graph, condLiteral);
            ConnectValue(graph, condLiteral.valueOutputs["output"], ifUnit.valueInputs["condition"]);

            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");

            Assert.AreEqual(1, ir.Methods.Count);
            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRIf>(body.Statements[0]);

            var code = CSharpEmitter.Emit(ir);
            StringAssert.Contains("if (true)", code);
        }

        [Test]
        public void Read_ForLoop_GeneratesForStatement()
        {
            var graph = CreateGraph();

            var start = new Start();
            AddAndDefine(graph, start);

            var forUnit = new For();
            AddAndDefine(graph, forUnit);

            ConnectControl(graph, start.controlOutputs["trigger"], forUnit.controlInputs["enter"]);

            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");

            Assert.AreEqual(1, ir.Methods.Count);
            var body = ir.Methods[0].Body;
            Assert.IsTrue(body.Statements.Count > 0);
            Assert.IsInstanceOf<IRFor>(body.Statements[0]);

            var code = CSharpEmitter.Emit(ir);
            StringAssert.Contains("for (", code);
        }

        [Test]
        public void Read_GraphVariables_GeneratesFields()
        {
            var graph = CreateGraph();
            graph.variables.Set("speed", 5f);

            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");

            Assert.AreEqual(1, ir.Fields.Count);
            Assert.AreEqual("speed", ir.Fields[0].Name);
        }

        [Test]
        public void Read_StartWithIfAndSetVar_ProducesValidCSharp()
        {
            var graph = CreateGraph();

            var start = new Start();
            AddAndDefine(graph, start);

            var ifUnit = new If();
            AddAndDefine(graph, ifUnit);
            ConnectControl(graph, start.controlOutputs["trigger"], ifUnit.controlInputs["enter"]);

            var condLiteral = new Literal(typeof(bool), true);
            AddAndDefine(graph, condLiteral);
            ConnectValue(graph, condLiteral.valueOutputs["output"], ifUnit.valueInputs["condition"]);

            var setVar = new SetVariable();
            AddAndDefine(graph, setVar);
            ConnectControl(graph, ifUnit.controlOutputs["ifTrue"], setVar.controlInputs["assign"]);

            var nameLiteral = new Literal(typeof(string), "active");
            AddAndDefine(graph, nameLiteral);
            ConnectValue(graph, nameLiteral.valueOutputs["output"], setVar.valueInputs["name"]);

            var valueLiteral = new Literal(typeof(bool), true);
            AddAndDefine(graph, valueLiteral);
            ConnectValue(graph, valueLiteral.valueOutputs["output"], setVar.valueInputs["input"]);

            var reader = new GraphReader();
            var ir = reader.Read(graph, "TestScript");
            var code = CSharpEmitter.Emit(ir);

            StringAssert.Contains("using UnityEngine;", code);
            StringAssert.Contains("public class TestScript : MonoBehaviour", code);
            StringAssert.Contains("private void Start()", code);
            StringAssert.Contains("if (true)", code);
            StringAssert.Contains("active = true", code);
        }
    }
}
