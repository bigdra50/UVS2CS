using NUnit.Framework;
using UVS2CS.CSharpToIR;
using UVS2CS.IR;

namespace UVS2CS.Tests.CSharpToIR
{
    public class CSharpParserTests
    {
        readonly CSharpParser _parser = new();

        [Test]
        public void Parse_SimpleClass_ExtractsClassName()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start() { }
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual("TestScript", ir.ClassName);
            Assert.AreEqual(1, ir.Methods.Count);
            Assert.AreEqual("Start", ir.Methods[0].Name);
            Assert.AreEqual(MethodKind.Lifecycle, ir.Methods[0].Kind);
        }

        [Test]
        public void Parse_FieldWithDefault_ExtractsField()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private float _speed = 5f;
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual(1, ir.Fields.Count);
            Assert.AreEqual("_speed", ir.Fields[0].Name);
            Assert.IsNotNull(ir.Fields[0].DefaultValue);
            Assert.IsInstanceOf<IRLiteral>(ir.Fields[0].DefaultValue);
        }

        [Test]
        public void Parse_SerializeFieldAttribute_DetectsModifier()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    [SerializeField]
    private int _health = 100;
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual(1, ir.Fields.Count);
            Assert.AreEqual(FieldModifier.SerializeField, ir.Fields[0].Modifier);
        }

        [Test]
        public void Parse_IfStatement_GeneratesIRIf()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Update()
    {
        if (true)
        {
            Debug.Log(""hit"");
        }
    }
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual(1, ir.Methods.Count);
            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRIf>(body.Statements[0]);
        }

        [Test]
        public void Parse_ForLoop_GeneratesIRFor()
        {
            const string source = @"
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
            var ir = _parser.Parse(source);

            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRFor>(body.Statements[0]);

            var forStmt = (IRFor)body.Statements[0];
            Assert.AreEqual("i", forStmt.IndexVariable);
        }

        [Test]
        public void Parse_ForEachLoop_GeneratesIRForEach()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
        foreach (var item in items)
        {
        }
    }
}";
            var ir = _parser.Parse(source);

            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRForEach>(body.Statements[0]);

            var forEach = (IRForEach)body.Statements[0];
            Assert.AreEqual("item", forEach.ItemVariable);
        }

        [Test]
        public void Parse_MethodCall_GeneratesIRMethodCall()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start()
    {
        Debug.Log(""Hello"");
    }
}";
            var ir = _parser.Parse(source);

            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRExpressionStatement>(body.Statements[0]);

            var exprStmt = (IRExpressionStatement)body.Statements[0];
            Assert.IsInstanceOf<IRMethodCall>(exprStmt.Expression);

            var call = (IRMethodCall)exprStmt.Expression;
            Assert.AreEqual("Log", call.MethodName);
            Assert.AreEqual(1, call.Arguments.Count);
        }

        [Test]
        public void Parse_Assignment_GeneratesIRAssignment()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private int _score;
    private void Start()
    {
        _score = 100;
    }
}";
            var ir = _parser.Parse(source);

            var body = ir.Methods[0].Body;
            Assert.AreEqual(1, body.Statements.Count);
            Assert.IsInstanceOf<IRAssignment>(body.Statements[0]);

            var assign = (IRAssignment)body.Statements[0];
            Assert.IsInstanceOf<IRIdentifier>(assign.Target);
            Assert.AreEqual("_score", ((IRIdentifier)assign.Target).Name);
        }

        [Test]
        public void Parse_Namespace_ExtractsNamespace()
        {
            const string source = @"
using UnityEngine;
namespace MyGame
{
    public class TestScript : MonoBehaviour
    {
    }
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual("MyGame", ir.Namespace);
        }

        [Test]
        public void Parse_MultipleLifecycleMethods_AllDetected()
        {
            const string source = @"
using UnityEngine;
public class TestScript : MonoBehaviour
{
    private void Start() { }
    private void Update() { }
    private void OnDestroy() { }
}";
            var ir = _parser.Parse(source);

            Assert.AreEqual(3, ir.Methods.Count);
            Assert.IsTrue(ir.Methods.TrueForAll(m => m.Kind == MethodKind.Lifecycle));
        }
    }
}
