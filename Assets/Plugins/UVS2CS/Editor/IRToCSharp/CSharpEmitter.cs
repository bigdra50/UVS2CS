using System.Collections.Generic;
using System.Linq;
using UVS2CS.IR;

namespace UVS2CS.IRToCSharp
{
    public static class CSharpEmitter
    {
        public static string Emit(IRGraph graph)
        {
            var w = new IndentWriter();

            var usings = CollectUsings(graph);
            foreach (var ns in usings)
                w.WriteLine($"using {ns};");

            if (usings.Count > 0)
                w.WriteLine();

            if (!string.IsNullOrEmpty(graph.Namespace))
            {
                w.WriteLine($"namespace {graph.Namespace}");
                w.OpenBrace();
            }

            w.WriteLine($"public class {graph.ClassName} : MonoBehaviour");
            w.OpenBrace();

            EmitFields(graph.Fields, w);

            for (var i = 0; i < graph.Methods.Count; i++)
            {
                if (i > 0 || graph.Fields.Count > 0)
                    w.WriteLine();
                EmitMethod(graph.Methods[i], w);
            }

            w.CloseBrace();

            if (!string.IsNullOrEmpty(graph.Namespace))
                w.CloseBrace();

            return w.ToString();
        }

        static void EmitFields(List<IRField> fields, IndentWriter w)
        {
            foreach (var field in fields)
            {
                switch (field.Modifier)
                {
                    case FieldModifier.SerializeField:
                        w.WriteLine("[SerializeField]");
                        w.Write($"private {field.Type} {field.Name}");
                        break;
                    case FieldModifier.Public:
                        w.Write($"public {field.Type} {field.Name}");
                        break;
                    default:
                        w.Write($"private {field.Type} {field.Name}");
                        break;
                }

                if (field.DefaultValue != null)
                    w.Write($" = {ExpressionEmitter.Emit(field.DefaultValue)}");

                w.WriteLine(";");
            }
        }

        static void EmitMethod(IRMethod method, IndentWriter w)
        {
            var access = method.Access switch
            {
                AccessModifier.Public => "public",
                AccessModifier.Protected => "protected",
                _ => "private",
            };

            var returnType = method.ReturnType?.ShortName ?? "void";
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

            w.WriteLine($"{access} {returnType} {method.Name}({parameters})");
            w.OpenBrace();

            if (method.Body != null)
            {
                foreach (var stmt in method.Body.Statements)
                    StatementEmitter.Emit(stmt, w);
            }

            w.CloseBrace();
        }

        static List<string> CollectUsings(IRGraph graph)
        {
            var namespaces = new HashSet<string> { "UnityEngine" };

            foreach (var u in graph.Usings)
            {
                if (!string.IsNullOrEmpty(u.Namespace))
                    namespaces.Add(u.Namespace);
            }

            foreach (var field in graph.Fields)
            {
                if (!string.IsNullOrEmpty(field.Type?.Namespace) && field.Type.Namespace != "System")
                    namespaces.Add(field.Type.Namespace);
            }

            var sorted = namespaces.ToList();
            sorted.Sort((a, b) =>
            {
                var aSystem = a.StartsWith("System");
                var bSystem = b.StartsWith("System");
                if (aSystem != bSystem) return aSystem ? -1 : 1;

                var aUnity = a.StartsWith("Unity");
                var bUnity = b.StartsWith("Unity");
                if (aUnity != bUnity) return aUnity ? -1 : 1;

                return string.Compare(a, b, System.StringComparison.Ordinal);
            });

            return sorted;
        }
    }
}
