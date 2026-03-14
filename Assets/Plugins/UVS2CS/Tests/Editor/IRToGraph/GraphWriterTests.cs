using System.Linq;
using NUnit.Framework;
using Unity.VisualScripting;
using UVS2CS.IR;
using UVS2CS.IRToGraph;

namespace UVS2CS.Tests.IRToGraph
{
    public class GraphWriterTests
    {
        readonly GraphWriter _writer = new();

        [Test]
        public void Write_EmptyStartMethod_CreatesStartUnit()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock(),
            });

            var graph = _writer.Write(ir);

            var startUnits = graph.units.Where(u => u is Start).ToList();
            Assert.AreEqual(1, startUnits.Count);
        }

        [Test]
        public void Write_GraphVariable_SetsVariable()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Fields.Add(new IRField
            {
                Name = "score",
                Type = IRTypeRef.Int,
                Modifier = FieldModifier.Private,
                Origin = VariableOrigin.Graph,
                DefaultValue = new IRLiteral { Value = 0, Type = IRTypeRef.Int },
            });

            var graph = _writer.Write(ir);

            Assert.IsTrue(graph.variables.IsDefined("score"));
        }

        [Test]
        public void Write_StartWithIf_CreatesIfUnit()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock
                {
                    Statements =
                    {
                        new IRIf
                        {
                            Condition = new IRLiteral { Value = true, Type = IRTypeRef.Bool },
                            ThenBody = new IRBlock(),
                        },
                    },
                },
            });

            var graph = _writer.Write(ir);

            var ifUnits = graph.units.Where(u => u is If).ToList();
            Assert.AreEqual(1, ifUnits.Count);

            Assert.IsTrue(graph.controlConnections.Count > 0,
                "Start should be connected to If");
        }

        [Test]
        public void Write_StartWithForLoop_CreatesForUnit()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Methods.Add(new IRMethod
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

            var graph = _writer.Write(ir);

            var forUnits = graph.units.Where(u => u is For).ToList();
            Assert.AreEqual(1, forUnits.Count);
        }

        [Test]
        public void Write_Assignment_CreatesSetVariableUnit()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock
                {
                    Statements =
                    {
                        new IRAssignment
                        {
                            Target = new IRIdentifier { Name = "health" },
                            Value = new IRLiteral { Value = 100, Type = IRTypeRef.Int },
                        },
                    },
                },
            });

            var graph = _writer.Write(ir);

            var setVarUnits = graph.units.Where(u => u is SetVariable).ToList();
            Assert.AreEqual(1, setVarUnits.Count);

            Assert.IsTrue(graph.controlConnections.Count > 0,
                "Start should be connected to SetVariable");
        }

        [Test]
        public void Write_MultipleMethods_CreatesMultipleEventUnits()
        {
            var ir = new IRGraph { ClassName = "TestScript" };
            ir.Methods.Add(new IRMethod
            {
                Name = "Start",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock(),
            });
            ir.Methods.Add(new IRMethod
            {
                Name = "Update",
                Kind = MethodKind.Lifecycle,
                ReturnType = IRTypeRef.Void,
                Access = AccessModifier.Private,
                Body = new IRBlock(),
            });

            var graph = _writer.Write(ir);

            var startUnits = graph.units.Where(u => u is Start).ToList();
            var updateUnits = graph.units.Where(u => u is Update).ToList();
            Assert.AreEqual(1, startUnits.Count);
            Assert.AreEqual(1, updateUnits.Count);
        }
    }
}
