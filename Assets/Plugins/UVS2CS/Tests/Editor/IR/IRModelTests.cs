using NUnit.Framework;
using UVS2CS.IR;

namespace UVS2CS.Tests.IR
{
    public class IRModelTests
    {
        [Test]
        public void IRGraph_CanConstruct_WithClassNameAndMethod()
        {
            var graph = new IRGraph
            {
                ClassName = "TestScript",
            };

            var method = new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock(),
            };
            graph.Methods.Add(method);

            Assert.AreEqual("TestScript", graph.ClassName);
            Assert.AreEqual(1, graph.Methods.Count);
            Assert.AreEqual("Start", graph.Methods[0].Name);
        }

        [Test]
        public void IRTypeRef_Equals_ByFullName()
        {
            var a = new IRTypeRef { FullName = "System.Int32", ShortName = "int" };
            var b = new IRTypeRef { FullName = "System.Int32", ShortName = "int" };

            Assert.AreEqual(a, b);
        }

        [Test]
        public void IRTypeRef_FromType_ReturnsAlias()
        {
            var intRef = IRTypeRef.FromType(typeof(int));

            Assert.AreEqual("System.Int32", intRef.FullName);
            Assert.AreEqual("int", intRef.ShortName);
        }

        [Test]
        public void IRBlock_CanNestStatements()
        {
            var block = new IRBlock();
            block.Statements.Add(new IRExpressionStatement
            {
                Expression = new IRMethodCall
                {
                    MethodName = "Log",
                    DeclaringType = new IRTypeRef { FullName = "UnityEngine.Debug", ShortName = "Debug" },
                    IsStatic = true,
                    Arguments = { new IRLiteral { Value = "Hello", Type = IRTypeRef.String } },
                },
            });
            block.Statements.Add(new IRIf
            {
                Condition = new IRLiteral { Value = true, Type = IRTypeRef.Bool },
                ThenBody = new IRBlock(),
            });

            Assert.AreEqual(2, block.Statements.Count);
            Assert.IsInstanceOf<IRExpressionStatement>(block.Statements[0]);
            Assert.IsInstanceOf<IRIf>(block.Statements[1]);
        }
    }
}
