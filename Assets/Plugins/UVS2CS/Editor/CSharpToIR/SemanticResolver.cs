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
                "void" or "System.Void" => "void",
                "int" or "System.Int32" => "int",
                "float" or "System.Single" => "float",
                "double" or "System.Double" => "double",
                "bool" or "System.Boolean" => "bool",
                "string" or "System.String" => "string",
                "object" or "System.Object" => "object",
                "System.Byte" => "byte",
                "System.Char" => "char",
                "System.Int64" => "long",
                "System.Int16" => "short",
                _ => symbol.Name,
            };
        }
    }
}
