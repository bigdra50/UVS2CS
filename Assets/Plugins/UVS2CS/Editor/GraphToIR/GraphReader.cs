using System.Collections.Generic;
using Unity.VisualScripting;
using UVS2CS.GraphToIR.UnitHandlers;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR
{
    public sealed class GraphReader
    {
        readonly UnitHandlerRegistry _registry;

        public GraphReader() : this(UnitHandlerRegistry.CreateDefault()) { }

        public GraphReader(UnitHandlerRegistry registry)
        {
            _registry = registry;
        }

        public IRGraph Read(ScriptGraphAsset asset)
        {
            return Read(asset.graph, asset.name);
        }

        public IRGraph Read(FlowGraph graph, string className)
        {
            var irGraph = new IRGraph
            {
                ClassName = SanitizeClassName(className),
            };
            irGraph.Usings.Add(new IRUsing { Namespace = "UnityEngine" });

            ReadVariables(graph, irGraph);
            ReadEventMethods(graph, irGraph);

            return irGraph;
        }

        void ReadVariables(FlowGraph graph, IRGraph irGraph)
        {
            foreach (var decl in graph.variables)
            {
                var field = new IRField
                {
                    Name = decl.name,
                    Type = IRTypeRef.FromType(decl.value?.GetType() ?? typeof(object)),
                    Modifier = FieldModifier.Private,
                    Origin = VariableOrigin.Graph,
                };

                if (decl.value != null)
                {
                    field.DefaultValue = new IRLiteral
                    {
                        Value = decl.value,
                        Type = field.Type,
                    };
                }

                irGraph.Fields.Add(field);
            }
        }

        void ReadEventMethods(FlowGraph graph, IRGraph irGraph)
        {
            var resolver = new ValueResolver(_registry);
            resolver.AnalyzeFanOut(graph);

            var tracer = new FlowTracer(_registry, resolver);
            tracer.SetGraph(graph);

            var methodNameCounts = new Dictionary<string, int>();

            foreach (var unit in graph.units)
            {
                if (!unit.isControlRoot) continue;

                var methodName = EventHandlers.GetMethodName(unit);

                // 同名メソッドの重複回避
                methodNameCounts.TryGetValue(methodName, out var count);
                methodNameCounts[methodName] = count + 1;
                if (count > 0) methodName = $"{methodName}_{count}";
                var triggerPort = EventHandlers.GetTriggerPort(unit);
                if (triggerPort == null) continue;

                var body = tracer.TraceFrom(triggerPort);

                if (resolver.PreambleStatements.Count > 0)
                {
                    foreach (var preamble in resolver.PreambleStatements)
                        body.Statements.Insert(0, preamble);
                    resolver.PreambleStatements.Clear();
                }

                var method = new IRMethod
                {
                    Name = methodName,
                    Kind = MethodKind.Lifecycle,
                    ReturnType = IRTypeRef.Void,
                    Access = AccessModifier.Private,
                    Body = body,
                };

                irGraph.Methods.Add(method);
            }
        }

        static string SanitizeClassName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "GeneratedScript";
            var sanitized = name.Replace(" ", "").Replace("-", "_");
            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;
            return sanitized;
        }
    }
}
