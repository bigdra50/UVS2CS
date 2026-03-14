using System.Globalization;
using UVS2CS.IR;

namespace UVS2CS.IRToCSharp
{
    public static class ExpressionEmitter
    {
        public static string Emit(IRExpression expr)
        {
            return expr switch
            {
                IRLiteral lit => EmitLiteral(lit),
                IRIdentifier id => id.Name,
                IRThis => "gameObject",
                IRNull => "null",
                IRMemberAccess ma => $"{Emit(ma.Target)}.{ma.MemberName}",
                IRMethodCall mc => EmitMethodCall(mc),
                IRConstructorCall cc => EmitConstructorCall(cc),
                IRBinaryOp bin => EmitBinaryOp(bin),
                IRUnaryOp un => EmitUnaryOp(un),
                IRCast cast => $"({cast.TargetType}){Emit(cast.Operand)}",
                IRNullCheck nc => nc.IsNull
                    ? $"{Emit(nc.Value)} == null"
                    : $"{Emit(nc.Value)} != null",
                IRNullCoalesce coal => $"{Emit(coal.Left)} ?? {Emit(coal.Fallback)}",
                IRConditional cond => $"{Emit(cond.Condition)} ? {Emit(cond.WhenTrue)} : {Emit(cond.WhenFalse)}",
                IRIndexAccess idx => $"{Emit(idx.Target)}[{Emit(idx.Index)}]",
                _ => "/* unsupported expression */",
            };
        }

        static string EmitLiteral(IRLiteral lit)
        {
            if (lit.Value == null) return "null";

            return lit.Value switch
            {
                bool b => b ? "true" : "false",
                int i => i.ToString(),
                long l => $"{l}L",
                float f => f.ToString("G", CultureInfo.InvariantCulture) + "f",
                double d => d.ToString("G", CultureInfo.InvariantCulture),
                string s => $"\"{EscapeString(s)}\"",
                char c => $"'{EscapeChar(c)}'",
                _ => lit.Value.ToString(),
            };
        }

        static string EmitMethodCall(IRMethodCall mc)
        {
            var args = string.Join(", ", mc.Arguments.ConvertAll(Emit));

            if (mc.IsStatic && mc.DeclaringType != null)
                return $"{mc.DeclaringType.ShortName}.{mc.MethodName}({args})";

            if (mc.Target != null)
                return $"{Emit(mc.Target)}.{mc.MethodName}({args})";

            return $"{mc.MethodName}({args})";
        }

        static string EmitConstructorCall(IRConstructorCall cc)
        {
            var args = string.Join(", ", cc.Arguments.ConvertAll(Emit));
            return $"new {cc.Type.ShortName}({args})";
        }

        static string EmitBinaryOp(IRBinaryOp bin)
        {
            var left = Emit(bin.Left);
            var right = Emit(bin.Right);
            var op = bin.Operator switch
            {
                BinaryOperator.Add => "+",
                BinaryOperator.Subtract => "-",
                BinaryOperator.Multiply => "*",
                BinaryOperator.Divide => "/",
                BinaryOperator.Modulo => "%",
                BinaryOperator.And => "&&",
                BinaryOperator.Or => "||",
                BinaryOperator.Xor => "^",
                BinaryOperator.Equal => "==",
                BinaryOperator.NotEqual => "!=",
                BinaryOperator.Greater => ">",
                BinaryOperator.GreaterOrEqual => ">=",
                BinaryOperator.Less => "<",
                BinaryOperator.LessOrEqual => "<=",
                _ => "??",
            };

            return NeedsParentheses(bin)
                ? $"({left} {op} {right})"
                : $"{left} {op} {right}";
        }

        static string EmitUnaryOp(IRUnaryOp un)
        {
            var operand = Emit(un.Operand);
            return un.Operator switch
            {
                UnaryOperator.Negate => $"-{operand}",
                UnaryOperator.LogicalNot => $"!{operand}",
                _ => operand,
            };
        }

        static bool NeedsParentheses(IRBinaryOp bin)
        {
            return bin.Left is IRBinaryOp || bin.Right is IRBinaryOp;
        }

        static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        static string EscapeChar(char c)
        {
            return c switch
            {
                '\'' => "\\'",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            };
        }
    }
}
