using System;
using System.Collections.Generic;

namespace UVS2CS.IR
{
    public sealed class IRTypeRef : IEquatable<IRTypeRef>
    {
        public string FullName { get; set; }
        public string ShortName { get; set; }
        public string Namespace { get; set; }
        public bool IsArray { get; set; }
        public IRTypeRef ElementType { get; set; }
        public List<IRTypeRef> GenericArguments { get; } = new();
        public Type ResolvedType { get; set; }

        public static IRTypeRef Void => new() { FullName = "System.Void", ShortName = "void" };
        public static IRTypeRef Int => new() { FullName = "System.Int32", ShortName = "int" };
        public static IRTypeRef Float => new() { FullName = "System.Single", ShortName = "float" };
        public static IRTypeRef Bool => new() { FullName = "System.Boolean", ShortName = "bool" };
        public static IRTypeRef String => new() { FullName = "System.String", ShortName = "string" };
        public static IRTypeRef Object => new() { FullName = "System.Object", ShortName = "object" };
        public static IRTypeRef GameObject => new() { FullName = "UnityEngine.GameObject", ShortName = "GameObject", Namespace = "UnityEngine" };
        public static IRTypeRef Vector3 => new() { FullName = "UnityEngine.Vector3", ShortName = "Vector3", Namespace = "UnityEngine" };

        public static IRTypeRef FromName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return Object;

            var parts = fullName.Split('.');
            var shortName = parts[parts.Length - 1];
            var ns = parts.Length > 1
                ? string.Join(".", parts, 0, parts.Length - 1)
                : null;

            return new IRTypeRef
            {
                FullName = fullName,
                ShortName = shortName,
                Namespace = ns,
            };
        }

        public static IRTypeRef FromType(Type type)
        {
            if (type == null) return Object;

            var shortName = GetCSharpAlias(type);
            return new IRTypeRef
            {
                FullName = type.FullName,
                ShortName = shortName ?? type.Name,
                Namespace = type.Namespace,
                IsArray = type.IsArray,
                ElementType = type.IsArray ? FromType(type.GetElementType()) : null,
                ResolvedType = type,
            };
        }

        static string GetCSharpAlias(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(char)) return "char";
            if (type == typeof(long)) return "long";
            if (type == typeof(short)) return "short";
            return null;
        }

        public bool Equals(IRTypeRef other)
        {
            if (other is null) return false;
            return FullName == other.FullName;
        }

        public override bool Equals(object obj) => Equals(obj as IRTypeRef);
        public override int GetHashCode() => FullName?.GetHashCode() ?? 0;
        public override string ToString() => ShortName ?? FullName ?? "unknown";
    }
}
