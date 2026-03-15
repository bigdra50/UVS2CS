using System.Linq;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UVS2CS.CSharpToIR;
using UVS2CS.GraphToIR;
using UVS2CS.IR;
using UVS2CS.IRToCSharp;
using UVS2CS.IRToGraph;

namespace UVS2CS.Tests.RoundTrip
{
    public class RoundTripTests
    {
        // C# → IR → C#: 構造が保存されることを検証
        [Test]
        public void CSharp_RoundTrip_SimpleStart_PreservesStructure()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("public class TestScript : MonoBehaviour", emitted);
            StringAssert.Contains("void Start()", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_FieldsPreserved()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private float _speed = 5f;
    private int _health = 100;

    private void Start()
    {
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("_speed", emitted);
            StringAssert.Contains("_health", emitted);
            StringAssert.Contains("5f", emitted);
            StringAssert.Contains("100", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_IfStatement_PreservesCondition()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Update()
    {
        if (true)
        {
        }
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("if (true)", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_ForLoop_PreservesIndexVar()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
        for (int i = 0; i < 10; i++)
        {
        }
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("for (", emitted);
            StringAssert.Contains("i", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_Assignment_PreservesTarget()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private int _score;
    private void Start()
    {
        _score = 42;
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("_score = 42", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_MethodCall_PreservesCallSite()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
        Debug.Log(""hello"");
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("Debug.Log(\"hello\")", emitted);
        }

        [Test]
        public void CSharp_RoundTrip_MultipleMethods_AllPreserved()
        {
            const string original = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
    }
    private void Update()
    {
    }
    private void OnDestroy()
    {
    }
}";
            var parser = new CSharpParser();
            var ir = parser.Parse(original);
            var emitted = CSharpEmitter.Emit(ir);

            StringAssert.Contains("void Start()", emitted);
            StringAssert.Contains("void Update()", emitted);
            StringAssert.Contains("void OnDestroy()", emitted);
        }

        // Graph → IR → Graph: ユニット構造が保存されることを検証
        [Test]
        public void Graph_RoundTrip_StartWithIf_PreservesUnitTypes()
        {
            var originalGraph = new FlowGraph();

            var start = new Start();
            start.position = Vector2.zero;
            originalGraph.units.Add(start);
            start.Define();

            var ifUnit = new If();
            ifUnit.position = new Vector2(300, 0);
            originalGraph.units.Add(ifUnit);
            ifUnit.Define();

            originalGraph.controlConnections.Add(
                new ControlConnection(start.controlOutputs["trigger"], ifUnit.controlInputs["enter"]));

            var condLiteral = new Literal(typeof(bool), true);
            condLiteral.position = new Vector2(150, 100);
            originalGraph.units.Add(condLiteral);
            condLiteral.Define();

            originalGraph.valueConnections.Add(
                new ValueConnection(condLiteral.valueOutputs["output"], ifUnit.valueInputs["condition"]));

            // Graph → IR
            var reader = new GraphReader();
            var ir = reader.Read(originalGraph, "TestScript");

            // IR → Graph
            var writer = new GraphWriter();
            var reconstructed = writer.Write(ir);

            // 検証: Start と If ユニットが存在する
            Assert.IsTrue(reconstructed.units.Any(u => u is Start), "Reconstructed graph should have Start");
            Assert.IsTrue(reconstructed.units.Any(u => u is If), "Reconstructed graph should have If");

            // 検証: 制御接続がある
            Assert.IsTrue(reconstructed.controlConnections.Count > 0,
                "Reconstructed graph should have control connections");
        }

        [Test]
        public void Graph_RoundTrip_Variables_Preserved()
        {
            var originalGraph = new FlowGraph();
            originalGraph.variables.Set("speed", 5f);
            originalGraph.variables.Set("name", "player");

            var reader = new GraphReader();
            var ir = reader.Read(originalGraph, "TestScript");

            var writer = new GraphWriter();
            var reconstructed = writer.Write(ir);

            Assert.IsTrue(reconstructed.variables.IsDefined("speed"));
            Assert.IsTrue(reconstructed.variables.IsDefined("name"));
        }

        // C# → IR → Graph → IR → C#: フルラウンドトリップ
        [Test]
        public void FullRoundTrip_CSharp_To_Graph_To_CSharp_PreservesClassName()
        {
            const string original = @"
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    private void Start()
    {
    }
}";
            // C# → IR
            var parser = new CSharpParser();
            var ir1 = parser.Parse(original);

            // IR → Graph
            var graphWriter = new GraphWriter();
            var graph = graphWriter.Write(ir1);

            // Graph → IR
            var graphReader = new GraphReader();
            var ir2 = graphReader.Read(graph, ir1.ClassName);

            // IR → C#
            var emitted = CSharpEmitter.Emit(ir2);

            StringAssert.Contains("public class PlayerController : MonoBehaviour", emitted);
            StringAssert.Contains("void Start()", emitted);
        }
    }
}
