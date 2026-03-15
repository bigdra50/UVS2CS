using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UVS2CS.IR;
using IRMethodKind = UVS2CS.IR.MethodKind;

namespace UVS2CS.CSharpToIR
{
    public sealed class CSharpParser
    {
        public IRGraph Parse(string source)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null)
                return new IRGraph { ClassName = "Unknown" };

            SemanticResolver resolver;
            try
            {
                var compilation = CreateCompilation(tree);
                var model = compilation.GetSemanticModel(tree);
                resolver = new SemanticResolver(model);
            }
            catch
            {
                resolver = new SemanticResolver(null);
            }

            var walker = new SyntaxWalker(resolver);

            var graph = new IRGraph
            {
                ClassName = classDecl.Identifier.Text,
            };

            ExtractUsings(root, graph);
            ExtractNamespace(classDecl, graph);
            ExtractFields(classDecl, graph, resolver);
            ExtractMethods(classDecl, graph, walker, resolver);

            return graph;
        }

        static void ExtractUsings(CompilationUnitSyntax root, IRGraph graph)
        {
            foreach (var usingDir in root.Usings)
            {
                var ns = usingDir.Name?.ToString();
                if (!string.IsNullOrEmpty(ns))
                    graph.Usings.Add(new IRUsing { Namespace = ns });
            }
        }

        static void ExtractNamespace(ClassDeclarationSyntax classDecl, IRGraph graph)
        {
            var nsDecl = classDecl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null)
                graph.Namespace = nsDecl.Name.ToString();
        }

        static void ExtractFields(ClassDeclarationSyntax classDecl, IRGraph graph, SemanticResolver resolver)
        {
            foreach (var fieldDecl in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                var type = resolver.ResolveType(fieldDecl.Declaration.Type);
                var modifier = GetFieldModifier(fieldDecl);

                foreach (var variable in fieldDecl.Declaration.Variables)
                {
                    var field = new IRField
                    {
                        Name = variable.Identifier.Text,
                        Type = type,
                        Modifier = modifier,
                        Origin = VariableOrigin.Graph,
                    };

                    if (variable.Initializer != null)
                    {
                        var walker = new SyntaxWalker(resolver);
                        field.DefaultValue = walker.ConvertExpression(variable.Initializer.Value);
                    }

                    graph.Fields.Add(field);
                }
            }
        }

        static void ExtractMethods(ClassDeclarationSyntax classDecl, IRGraph graph,
            SyntaxWalker walker, SemanticResolver resolver)
        {
            foreach (var methodDecl in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var methodName = methodDecl.Identifier.Text;
                var isLifecycle = MonoBehaviourDetector.IsLifecycleMethod(methodName);

                var method = new IRMethod
                {
                    Name = methodName,
                    Kind = isLifecycle ? IRMethodKind.Lifecycle : IRMethodKind.Custom,
                    ReturnType = resolver.ResolveType(methodDecl.ReturnType),
                    Access = GetAccessModifier(methodDecl),
                    Body = walker.ConvertBlock(methodDecl.Body),
                };

                foreach (var param in methodDecl.ParameterList.Parameters)
                {
                    method.Parameters.Add(new IRParameter
                    {
                        Name = param.Identifier.Text,
                        Type = resolver.ResolveType(param.Type),
                    });
                }

                graph.Methods.Add(method);
            }
        }

        static FieldModifier GetFieldModifier(FieldDeclarationSyntax field)
        {
            if (field.AttributeLists.Any(al =>
                al.Attributes.Any(a => a.Name.ToString().Contains("SerializeField"))))
                return FieldModifier.SerializeField;

            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                return FieldModifier.Public;

            return FieldModifier.Private;
        }

        static AccessModifier GetAccessModifier(MethodDeclarationSyntax method)
        {
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                return AccessModifier.Public;
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
                return AccessModifier.Protected;
            return AccessModifier.Private;
        }

        static CSharpCompilation CreateCompilation(SyntaxTree tree)
        {
            var references = new List<MetadataReference>();

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                try
                {
                    if (!string.IsNullOrEmpty(asm.Location))
                        references.Add(MetadataReference.CreateFromFile(asm.Location));
                }
                catch { /* skip assemblies we can't reference */ }
            }

            return CSharpCompilation.Create("UVS2CSAnalysis",
                syntaxTrees: new[] { tree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
