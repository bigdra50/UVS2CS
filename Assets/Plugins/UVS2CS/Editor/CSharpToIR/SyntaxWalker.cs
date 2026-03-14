using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UVS2CS.IR;

namespace UVS2CS.CSharpToIR
{
    public sealed class SyntaxWalker
    {
        readonly SemanticResolver _resolver;

        public SyntaxWalker(SemanticResolver resolver)
        {
            _resolver = resolver;
        }

        public IRBlock ConvertBlock(BlockSyntax block)
        {
            if (block == null) return new IRBlock();

            var irBlock = new IRBlock();
            foreach (var stmt in block.Statements)
            {
                var converted = ConvertStatement(stmt);
                if (converted != null)
                    irBlock.Statements.Add(converted);
            }
            return irBlock;
        }

        IRStatement ConvertStatement(StatementSyntax stmt)
        {
            return stmt switch
            {
                BlockSyntax block => ConvertBlock(block),
                ExpressionStatementSyntax expr => ConvertExpressionStatement(expr),
                LocalDeclarationStatementSyntax local => ConvertLocalDeclaration(local),
                IfStatementSyntax ifStmt => ConvertIf(ifStmt),
                ForStatementSyntax forStmt => ConvertFor(forStmt),
                ForEachStatementSyntax forEach => ConvertForEach(forEach),
                WhileStatementSyntax whileStmt => ConvertWhile(whileStmt),
                ReturnStatementSyntax ret => ConvertReturn(ret),
                BreakStatementSyntax => new IRBreak(),
                ThrowStatementSyntax throwStmt => new IRThrow
                {
                    Expression = throwStmt.Expression != null ? ConvertExpression(throwStmt.Expression) : null,
                },
                TryStatementSyntax tryStmt => ConvertTryCatch(tryStmt),
                SwitchStatementSyntax switchStmt => ConvertSwitch(switchStmt),
                YieldStatementSyntax yieldStmt => ConvertYield(yieldStmt),
                _ => null,
            };
        }

        IRStatement ConvertExpressionStatement(ExpressionStatementSyntax stmt)
        {
            var expr = stmt.Expression;

            if (expr is AssignmentExpressionSyntax assignment)
            {
                return new IRAssignment
                {
                    Target = ConvertExpression(assignment.Left),
                    Value = ConvertExpression(assignment.Right),
                };
            }

            return new IRExpressionStatement
            {
                Expression = ConvertExpression(expr),
            };
        }

        IRStatement ConvertLocalDeclaration(LocalDeclarationStatementSyntax local)
        {
            var decl = local.Declaration;
            var variable = decl.Variables.First();

            var typeSyntax = decl.Type;
            var isVar = typeSyntax.IsVar;

            return new IRVariableDeclaration
            {
                Name = variable.Identifier.Text,
                Type = isVar ? null : _resolver.ResolveType(typeSyntax),
                Initializer = variable.Initializer != null
                    ? ConvertExpression(variable.Initializer.Value)
                    : null,
            };
        }

        IRStatement ConvertIf(IfStatementSyntax ifStmt)
        {
            var condition = ConvertExpression(ifStmt.Condition);
            var thenBody = WrapInBlock(ifStmt.Statement);
            var elseBody = ifStmt.Else != null ? WrapInBlock(ifStmt.Else.Statement) : null;

            return new IRIf
            {
                Condition = condition,
                ThenBody = thenBody,
                ElseBody = elseBody,
            };
        }

        IRStatement ConvertFor(ForStatementSyntax forStmt)
        {
            var indexVar = "i";
            IRExpression first = new IRLiteral { Value = 0, Type = IRTypeRef.Int };
            IRExpression last = new IRLiteral { Value = 0, Type = IRTypeRef.Int };
            IRExpression step = new IRLiteral { Value = 1, Type = IRTypeRef.Int };

            if (forStmt.Declaration?.Variables.Count > 0)
            {
                var varDecl = forStmt.Declaration.Variables[0];
                indexVar = varDecl.Identifier.Text;
                if (varDecl.Initializer != null)
                    first = ConvertExpression(varDecl.Initializer.Value);
            }

            if (forStmt.Condition is BinaryExpressionSyntax binCond)
                last = ConvertExpression(binCond.Right);

            if (forStmt.Incrementors.Count > 0)
            {
                var inc = forStmt.Incrementors[0];
                if (inc is PostfixUnaryExpressionSyntax)
                    step = new IRLiteral { Value = 1, Type = IRTypeRef.Int };
                else if (inc is AssignmentExpressionSyntax assignInc)
                    step = ConvertExpression(assignInc.Right);
            }

            return new IRFor
            {
                IndexVariable = indexVar,
                First = first,
                Last = last,
                Step = step,
                Body = WrapInBlock(forStmt.Statement),
            };
        }

        IRStatement ConvertForEach(ForEachStatementSyntax forEach)
        {
            return new IRForEach
            {
                ItemVariable = forEach.Identifier.Text,
                ItemType = _resolver.ResolveType(forEach.Type),
                Collection = ConvertExpression(forEach.Expression),
                Body = WrapInBlock(forEach.Statement),
            };
        }

        IRStatement ConvertWhile(WhileStatementSyntax whileStmt)
        {
            return new IRWhile
            {
                Condition = ConvertExpression(whileStmt.Condition),
                Body = WrapInBlock(whileStmt.Statement),
            };
        }

        IRStatement ConvertReturn(ReturnStatementSyntax ret)
        {
            return new IRReturn
            {
                Value = ret.Expression != null ? ConvertExpression(ret.Expression) : null,
            };
        }

        IRStatement ConvertTryCatch(TryStatementSyntax tryStmt)
        {
            var catchClause = tryStmt.Catches.FirstOrDefault();

            return new IRTryCatch
            {
                TryBody = ConvertBlock(tryStmt.Block),
                ExceptionType = catchClause?.Declaration != null
                    ? _resolver.ResolveType(catchClause.Declaration.Type)
                    : null,
                ExceptionVariable = catchClause?.Declaration?.Identifier.Text,
                CatchBody = catchClause != null ? ConvertBlock(catchClause.Block) : null,
                FinallyBody = tryStmt.Finally != null ? ConvertBlock(tryStmt.Finally.Block) : null,
            };
        }

        IRStatement ConvertSwitch(SwitchStatementSyntax switchStmt)
        {
            var irSwitch = new IRSwitch
            {
                Value = ConvertExpression(switchStmt.Expression),
            };

            foreach (var section in switchStmt.Sections)
            {
                foreach (var label in section.Labels)
                {
                    if (label is CaseSwitchLabelSyntax caseLabel)
                    {
                        var body = new IRBlock();
                        foreach (var s in section.Statements)
                        {
                            if (s is BreakStatementSyntax) continue;
                            var converted = ConvertStatement(s);
                            if (converted != null) body.Statements.Add(converted);
                        }
                        irSwitch.Sections.Add(new IRSwitchSection
                        {
                            Label = ConvertExpression(caseLabel.Value),
                            Body = body,
                        });
                    }
                    else if (label is DefaultSwitchLabelSyntax)
                    {
                        var body = new IRBlock();
                        foreach (var s in section.Statements)
                        {
                            if (s is BreakStatementSyntax) continue;
                            var converted = ConvertStatement(s);
                            if (converted != null) body.Statements.Add(converted);
                        }
                        irSwitch.DefaultBody = body;
                    }
                }
            }

            return irSwitch;
        }

        IRStatement ConvertYield(YieldStatementSyntax yieldStmt)
        {
            return new IRYieldReturn
            {
                Expression = yieldStmt.Expression != null ? ConvertExpression(yieldStmt.Expression) : null,
            };
        }

        public IRExpression ConvertExpression(ExpressionSyntax expr)
        {
            return expr switch
            {
                LiteralExpressionSyntax literal => ConvertLiteral(literal),
                IdentifierNameSyntax id => new IRIdentifier { Name = id.Identifier.Text },
                ThisExpressionSyntax => new IRThis(),
                MemberAccessExpressionSyntax ma => ConvertMemberAccess(ma),
                InvocationExpressionSyntax invocation => ConvertInvocation(invocation),
                ObjectCreationExpressionSyntax creation => ConvertObjectCreation(creation),
                BinaryExpressionSyntax binary => ConvertBinary(binary),
                PrefixUnaryExpressionSyntax prefix => ConvertPrefixUnary(prefix),
                ParenthesizedExpressionSyntax paren => ConvertExpression(paren.Expression),
                CastExpressionSyntax cast => new IRCast
                {
                    Operand = ConvertExpression(cast.Expression),
                    TargetType = _resolver.ResolveType(cast.Type),
                },
                ConditionalExpressionSyntax cond => new IRConditional
                {
                    Condition = ConvertExpression(cond.Condition),
                    WhenTrue = ConvertExpression(cond.WhenTrue),
                    WhenFalse = ConvertExpression(cond.WhenFalse),
                },
                ElementAccessExpressionSyntax elemAccess => new IRIndexAccess
                {
                    Target = ConvertExpression(elemAccess.Expression),
                    Index = elemAccess.ArgumentList.Arguments.Count > 0
                        ? ConvertExpression(elemAccess.ArgumentList.Arguments[0].Expression)
                        : new IRLiteral { Value = 0, Type = IRTypeRef.Int },
                },
                _ => new IRIdentifier { Name = expr.ToString() },
            };
        }

        IRExpression ConvertLiteral(LiteralExpressionSyntax literal)
        {
            return literal.Kind() switch
            {
                SyntaxKind.NullLiteralExpression => new IRNull(),
                SyntaxKind.TrueLiteralExpression => new IRLiteral { Value = true, Type = IRTypeRef.Bool },
                SyntaxKind.FalseLiteralExpression => new IRLiteral { Value = false, Type = IRTypeRef.Bool },
                SyntaxKind.NumericLiteralExpression => new IRLiteral
                {
                    Value = literal.Token.Value,
                    Type = IRTypeRef.FromType(literal.Token.Value?.GetType()),
                },
                SyntaxKind.StringLiteralExpression => new IRLiteral
                {
                    Value = literal.Token.ValueText,
                    Type = IRTypeRef.String,
                },
                SyntaxKind.CharacterLiteralExpression => new IRLiteral
                {
                    Value = literal.Token.ValueText[0],
                    Type = IRTypeRef.FromType(typeof(char)),
                },
                _ => new IRLiteral { Value = literal.Token.Value, Type = IRTypeRef.Object },
            };
        }

        IRExpression ConvertMemberAccess(MemberAccessExpressionSyntax ma)
        {
            return new IRMemberAccess
            {
                Target = ConvertExpression(ma.Expression),
                MemberName = ma.Name.Identifier.Text,
            };
        }

        IRExpression ConvertInvocation(InvocationExpressionSyntax invocation)
        {
            var call = new IRMethodCall();

            if (invocation.Expression is MemberAccessExpressionSyntax ma)
            {
                call.Target = ConvertExpression(ma.Expression);
                call.MethodName = ma.Name.Identifier.Text;

                if (ma.Expression is IdentifierNameSyntax className && char.IsUpper(className.Identifier.Text[0]))
                {
                    call.IsStatic = true;
                    call.DeclaringType = new IRTypeRef
                    {
                        ShortName = className.Identifier.Text,
                        FullName = className.Identifier.Text,
                    };
                    call.Target = null;
                }
            }
            else if (invocation.Expression is IdentifierNameSyntax id)
            {
                call.MethodName = id.Identifier.Text;
            }

            foreach (var arg in invocation.ArgumentList.Arguments)
                call.Arguments.Add(ConvertExpression(arg.Expression));

            return call;
        }

        IRExpression ConvertObjectCreation(ObjectCreationExpressionSyntax creation)
        {
            var ctor = new IRConstructorCall
            {
                Type = _resolver.ResolveType(creation.Type),
            };

            if (creation.ArgumentList != null)
            {
                foreach (var arg in creation.ArgumentList.Arguments)
                    ctor.Arguments.Add(ConvertExpression(arg.Expression));
            }

            return ctor;
        }

        IRExpression ConvertBinary(BinaryExpressionSyntax binary)
        {
            var op = binary.Kind() switch
            {
                SyntaxKind.AddExpression => IR.BinaryOperator.Add,
                SyntaxKind.SubtractExpression => IR.BinaryOperator.Subtract,
                SyntaxKind.MultiplyExpression => IR.BinaryOperator.Multiply,
                SyntaxKind.DivideExpression => IR.BinaryOperator.Divide,
                SyntaxKind.ModuloExpression => IR.BinaryOperator.Modulo,
                SyntaxKind.LogicalAndExpression => IR.BinaryOperator.And,
                SyntaxKind.LogicalOrExpression => IR.BinaryOperator.Or,
                SyntaxKind.ExclusiveOrExpression => IR.BinaryOperator.Xor,
                SyntaxKind.EqualsExpression => IR.BinaryOperator.Equal,
                SyntaxKind.NotEqualsExpression => IR.BinaryOperator.NotEqual,
                SyntaxKind.GreaterThanExpression => IR.BinaryOperator.Greater,
                SyntaxKind.GreaterThanOrEqualExpression => IR.BinaryOperator.GreaterOrEqual,
                SyntaxKind.LessThanExpression => IR.BinaryOperator.Less,
                SyntaxKind.LessThanOrEqualExpression => IR.BinaryOperator.LessOrEqual,
                _ => IR.BinaryOperator.Add,
            };

            return new IRBinaryOp
            {
                Left = ConvertExpression(binary.Left),
                Right = ConvertExpression(binary.Right),
                Operator = op,
            };
        }

        IRExpression ConvertPrefixUnary(PrefixUnaryExpressionSyntax prefix)
        {
            var op = prefix.Kind() switch
            {
                SyntaxKind.UnaryMinusExpression => IR.UnaryOperator.Negate,
                SyntaxKind.LogicalNotExpression => IR.UnaryOperator.LogicalNot,
                _ => IR.UnaryOperator.Negate,
            };

            return new IRUnaryOp
            {
                Operand = ConvertExpression(prefix.Operand),
                Operator = op,
            };
        }

        IRBlock WrapInBlock(StatementSyntax stmt)
        {
            if (stmt is BlockSyntax block)
                return ConvertBlock(block);

            var irBlock = new IRBlock();
            var converted = ConvertStatement(stmt);
            if (converted != null)
                irBlock.Statements.Add(converted);
            return irBlock;
        }
    }
}
