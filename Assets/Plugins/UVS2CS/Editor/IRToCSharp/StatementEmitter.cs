using UVS2CS.IR;

namespace UVS2CS.IRToCSharp
{
    public static class StatementEmitter
    {
        public static void Emit(IRStatement stmt, IndentWriter w)
        {
            switch (stmt)
            {
                case IRBlock block:
                    EmitBlock(block, w);
                    break;
                case IRExpressionStatement expr:
                    w.WriteLine($"{ExpressionEmitter.Emit(expr.Expression)};");
                    break;
                case IRAssignment assign:
                    w.WriteLine($"{ExpressionEmitter.Emit(assign.Target)} = {ExpressionEmitter.Emit(assign.Value)};");
                    break;
                case IRVariableDeclaration decl:
                    EmitVariableDeclaration(decl, w);
                    break;
                case IRIf ifStmt:
                    EmitIf(ifStmt, w);
                    break;
                case IRFor forStmt:
                    EmitFor(forStmt, w);
                    break;
                case IRForEach forEach:
                    EmitForEach(forEach, w);
                    break;
                case IRWhile whileStmt:
                    EmitWhile(whileStmt, w);
                    break;
                case IRReturn ret:
                    w.WriteLine(ret.Value != null
                        ? $"return {ExpressionEmitter.Emit(ret.Value)};"
                        : "return;");
                    break;
                case IRBreak:
                    w.WriteLine("break;");
                    break;
                case IRThrow throwStmt:
                    w.WriteLine(throwStmt.Expression != null
                        ? $"throw {ExpressionEmitter.Emit(throwStmt.Expression)};"
                        : "throw;");
                    break;
                case IRTryCatch tryCatch:
                    EmitTryCatch(tryCatch, w);
                    break;
            }
        }

        static void EmitBlock(IRBlock block, IndentWriter w)
        {
            foreach (var stmt in block.Statements)
                Emit(stmt, w);
        }

        static void EmitVariableDeclaration(IRVariableDeclaration decl, IndentWriter w)
        {
            var typeName = decl.Type != null ? decl.Type.ShortName : "var";
            if (decl.Initializer != null)
                w.WriteLine($"{typeName} {decl.Name} = {ExpressionEmitter.Emit(decl.Initializer)};");
            else
                w.WriteLine($"{typeName} {decl.Name};");
        }

        static void EmitIf(IRIf ifStmt, IndentWriter w)
        {
            w.WriteLine($"if ({ExpressionEmitter.Emit(ifStmt.Condition)})");
            w.OpenBrace();
            if (ifStmt.ThenBody != null)
                EmitBlock(ifStmt.ThenBody, w);
            w.CloseBrace();

            if (ifStmt.ElseBody != null && ifStmt.ElseBody.Statements.Count > 0)
            {
                w.WriteLine("else");
                w.OpenBrace();
                EmitBlock(ifStmt.ElseBody, w);
                w.CloseBrace();
            }
        }

        static void EmitFor(IRFor forStmt, IndentWriter w)
        {
            var init = $"var {forStmt.IndexVariable} = {ExpressionEmitter.Emit(forStmt.First)}";
            var condition = $"{forStmt.IndexVariable} <= {ExpressionEmitter.Emit(forStmt.Last)}";
            var step = forStmt.Step is IRLiteral { Value: 1 }
                ? $"{forStmt.IndexVariable}++"
                : $"{forStmt.IndexVariable} += {ExpressionEmitter.Emit(forStmt.Step)}";

            w.WriteLine($"for ({init}; {condition}; {step})");
            w.OpenBrace();
            if (forStmt.Body != null)
                EmitBlock(forStmt.Body, w);
            w.CloseBrace();
        }

        static void EmitForEach(IRForEach forEach, IndentWriter w)
        {
            var typeName = forEach.ItemType != null ? forEach.ItemType.ShortName : "var";
            w.WriteLine($"foreach ({typeName} {forEach.ItemVariable} in {ExpressionEmitter.Emit(forEach.Collection)})");
            w.OpenBrace();
            if (forEach.Body != null)
                EmitBlock(forEach.Body, w);
            w.CloseBrace();
        }

        static void EmitWhile(IRWhile whileStmt, IndentWriter w)
        {
            w.WriteLine($"while ({ExpressionEmitter.Emit(whileStmt.Condition)})");
            w.OpenBrace();
            if (whileStmt.Body != null)
                EmitBlock(whileStmt.Body, w);
            w.CloseBrace();
        }

        static void EmitTryCatch(IRTryCatch tryCatch, IndentWriter w)
        {
            w.WriteLine("try");
            w.OpenBrace();
            if (tryCatch.TryBody != null)
                EmitBlock(tryCatch.TryBody, w);
            w.CloseBrace();

            if (tryCatch.CatchBody != null)
            {
                var exType = tryCatch.ExceptionType?.ShortName ?? "Exception";
                var exVar = tryCatch.ExceptionVariable ?? "ex";
                w.WriteLine($"catch ({exType} {exVar})");
                w.OpenBrace();
                EmitBlock(tryCatch.CatchBody, w);
                w.CloseBrace();
            }

            if (tryCatch.FinallyBody != null && tryCatch.FinallyBody.Statements.Count > 0)
            {
                w.WriteLine("finally");
                w.OpenBrace();
                EmitBlock(tryCatch.FinallyBody, w);
                w.CloseBrace();
            }
        }
    }
}
