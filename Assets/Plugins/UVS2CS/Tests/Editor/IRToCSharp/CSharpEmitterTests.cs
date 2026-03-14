using NUnit.Framework;
using UVS2CS.IR;
using UVS2CS.IRToCSharp;

namespace UVS2CS.Tests.IRToCSharp
{
    public class CSharpEmitterTests
    {
        [Test]
        public void Emit_SimpleStartMethod_GeneratesCorrectCode()
        {
            var graph = new IRGraph { ClassName = "TestScript" };
            graph.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock
                {
                    Statements =
                    {
                        new IRExpressionStatement
                        {
                            Expression = new IRMethodCall
                            {
                                MethodName = "Log",
                                DeclaringType = new IRTypeRef { FullName = "UnityEngine.Debug", ShortName = "Debug", Namespace = "UnityEngine" },
                                IsStatic = true,
                                Arguments = { new IRLiteral { Value = "Hello", Type = IRTypeRef.String } },
                            },
                        },
                    },
                },
            });

            var result = CSharpEmitter.Emit(graph);

            StringAssert.Contains("public class TestScript : MonoBehaviour", result);
            StringAssert.Contains("private void Start()", result);
            StringAssert.Contains("Debug.Log(\"Hello\");", result);
            StringAssert.Contains("using UnityEngine;", result);
        }

        [Test]
        public void Emit_FieldWithDefault_GeneratesFieldDeclaration()
        {
            var graph = new IRGraph { ClassName = "TestScript" };
            graph.Fields.Add(new IRField
            {
                Name = "_speed",
                Type = IRTypeRef.Float,
                Modifier = FieldModifier.SerializeField,
                DefaultValue = new IRLiteral { Value = 5.0f, Type = IRTypeRef.Float },
            });

            var result = CSharpEmitter.Emit(graph);

            StringAssert.Contains("[SerializeField]", result);
            StringAssert.Contains("private float _speed = 5f;", result);
        }

        [Test]
        public void Emit_IfStatement_GeneratesCorrectBranching()
        {
            var graph = new IRGraph { ClassName = "TestScript" };
            graph.Methods.Add(new IRMethod
            {
                Name = "Update",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock
                {
                    Statements =
                    {
                        new IRIf
                        {
                            Condition = new IRBinaryOp
                            {
                                Left = new IRIdentifier { Name = "_health" },
                                Right = new IRLiteral { Value = 0, Type = IRTypeRef.Int },
                                Operator = BinaryOperator.LessOrEqual,
                            },
                            ThenBody = new IRBlock
                            {
                                Statements =
                                {
                                    new IRExpressionStatement
                                    {
                                        Expression = new IRMethodCall
                                        {
                                            MethodName = "Log",
                                            DeclaringType = new IRTypeRef { FullName = "UnityEngine.Debug", ShortName = "Debug" },
                                            IsStatic = true,
                                            Arguments = { new IRLiteral { Value = "Dead", Type = IRTypeRef.String } },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            });

            var result = CSharpEmitter.Emit(graph);

            StringAssert.Contains("if (_health <= 0)", result);
            StringAssert.Contains("Debug.Log(\"Dead\");", result);
        }

        [Test]
        public void Emit_ForLoop_GeneratesCorrectLoop()
        {
            var graph = new IRGraph { ClassName = "TestScript" };
            graph.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock
                {
                    Statements =
                    {
                        new IRFor
                        {
                            IndexVariable = "i",
                            First = new IRLiteral { Value = 0, Type = IRTypeRef.Int },
                            Last = new IRLiteral { Value = 9, Type = IRTypeRef.Int },
                            Step = new IRLiteral { Value = 1, Type = IRTypeRef.Int },
                            Body = new IRBlock(),
                        },
                    },
                },
            });

            var result = CSharpEmitter.Emit(graph);

            StringAssert.Contains("for (var i = 0; i <= 9; i++)", result);
        }
    }
}
