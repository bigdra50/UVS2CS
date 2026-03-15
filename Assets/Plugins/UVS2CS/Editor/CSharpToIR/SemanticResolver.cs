#if UVS2CS_HAS_ROSLYN
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UVS2CS.IR;

namespace UVS2CS.CSharpToIR
{
    public sealed class SemanticResolver
    {
        readonly SemanticModel _model;

        public SemanticResolver(SemanticModel model)
        {
            _model = model;
        }

        public IRTypeRef ResolveType(TypeSyntax typeSyntax)
        {
            if (typeSyntax == null) return IRTypeRef.Object;

            if (_model != null)
            {
                var typeInfo = _model.GetTypeInfo(typeSyntax);
                if (typeInfo.Type != null)
                    return FromSymbol(typeInfo.Type);
            }

            return FromSyntax(typeSyntax);
        }

        public IRTypeRef ResolveExpression(ExpressionSyntax expr)
        {
            if (expr == null || _model == null) return IRTypeRef.Object;

            var typeInfo = _model.GetTypeInfo(expr);
            if (typeInfo.Type != null)
                return FromSymbol(typeInfo.Type);

            return IRTypeRef.Object;
        }

        static IRTypeRef FromSymbol(ITypeSymbol symbol)
        {
            var display = symbol.ToDisplayString();
            return new IRTypeRef
            {
                FullName = display,
                ShortName = GetShortName(display, symbol),
                Namespace = symbol.ContainingNamespace?.ToDisplayString(),
                IsArray = symbol is IArrayTypeSymbol,
            };
        }

        static IRTypeRef FromSyntax(TypeSyntax syntax)
        {
            var text = syntax.ToString();
            return new IRTypeRef
            {
                FullName = text,
                ShortName = text,
            };
        }

        static string GetShortName(string fullName, ITypeSymbol symbol)
        {
            return fullName switch
            {
                "System.Void" => "void",
                "System.Int32" => "int",
                "System.Single" => "float",
                "System.Double" => "double",
                "System.Boolean" => "bool",
                "System.String" => "string",
                "System.Object" => "object",
                "System.Byte" => "byte",
                "System.Char" => "char",
                "System.Int64" => "long",
                "System.Int16" => "short",
                _ => symbol.Name,
            };
        }
    }
}
#endif
