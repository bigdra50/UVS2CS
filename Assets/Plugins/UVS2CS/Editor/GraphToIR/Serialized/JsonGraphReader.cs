using System.Collections.Generic;
using System.Linq;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.Serialized
{
    /// <summary>
    /// SerializedGraphSnapshot から直接 IRGraph を構築する。
    /// Unity の FlowGraph API に依存せず、port key ベースで走査する。
    /// </summary>
    public sealed class JsonGraphReader
    {
        readonly SerializedGraphSnapshot _snapshot;
        readonly Dictionary<string, List<SerializedEdge>> _controlEdgesBySource = new();
        readonly Dictionary<string, List<SerializedEdge>> _valueEdgesByDest = new();

        public JsonGraphReader(SerializedGraphSnapshot snapshot)
        {
            _snapshot = snapshot;
            IndexEdges();
        }

        void IndexEdges()
        {
            foreach (var edge in _snapshot.ControlEdges)
            {
                var key = $"{edge.SourceUnitId}:{edge.SourceKey}";
                if (!_controlEdgesBySource.TryGetValue(key, out var list))
                {
                    list = new List<SerializedEdge>();
                    _controlEdgesBySource[key] = list;
                }
                list.Add(edge);
            }

            foreach (var edge in _snapshot.ValueEdges)
            {
                var key = $"{edge.DestUnitId}:{edge.DestKey}";
                if (!_valueEdgesByDest.TryGetValue(key, out var list))
                {
                    list = new List<SerializedEdge>();
                    _valueEdgesByDest[key] = list;
                }
                list.Add(edge);
            }
        }

        public IRGraph Read(string className)
        {
            var ir = new IRGraph
            {
                ClassName = SanitizeClassName(className),
            };
            ir.Usings.Add(new IRUsing { Namespace = "UnityEngine" });

            ReadVariables(ir);
            ReadEventMethods(ir);

            return ir;
        }

        void ReadVariables(IRGraph ir)
        {
            foreach (var kv in _snapshot.Variables)
            {
                var field = new IRField
                {
                    Name = kv.Key,
                    Type = kv.Value != null ? IRTypeRef.FromType(kv.Value.GetType()) : IRTypeRef.Object,
                    Modifier = FieldModifier.Private,
                    Origin = VariableOrigin.Graph,
                };

                if (kv.Value != null)
                {
                    field.DefaultValue = new IRLiteral
                    {
                        Value = kv.Value,
                        Type = field.Type,
                    };
                }

                ir.Fields.Add(field);
            }
        }

        void ReadEventMethods(IRGraph ir)
        {
            var methodNameCounts = new Dictionary<string, int>();

            foreach (var unit in _snapshot.Units.Values)
            {
                if (unit.Kind != UnitKind.Event && unit.Kind != UnitKind.CustomEvent)
                    continue;

                var methodName = GetMethodName(unit);
                methodNameCounts.TryGetValue(methodName, out var count);
                methodNameCounts[methodName] = count + 1;
                if (count > 0) methodName = $"{methodName}_{count}";

                var body = TraceControlFlow(unit.Id, "trigger");

                ir.Methods.Add(new IRMethod
                {
                    Name = methodName,
                    Kind = MethodKind.Lifecycle,
                    ReturnType = IRTypeRef.Void,
                    Access = AccessModifier.Private,
                    Body = body,
                });
            }
        }

        IRBlock TraceControlFlow(string unitId, string outputKey)
        {
            var block = new IRBlock();
            var currentUnitId = unitId;
            var currentKey = outputKey;

            var visited = new HashSet<string>();

            while (true)
            {
                var edgeKey = $"{currentUnitId}:{currentKey}";
                if (!_controlEdgesBySource.TryGetValue(edgeKey, out var edges) || edges.Count == 0)
                    break;

                var edge = edges[0];
                if (!_snapshot.Units.TryGetValue(edge.DestUnitId, out var destUnit))
                    break;

                var visitKey = $"{edge.DestUnitId}:{edge.DestKey}";
                if (!visited.Add(visitKey)) break;

                var stmt = ConvertUnit(destUnit);
                if (stmt != null) block.Statements.Add(stmt);

                currentUnitId = edge.DestUnitId;
                currentKey = GetContinuationKey(destUnit);
                if (currentKey == null) break;
            }

            return block;
        }

        IRStatement ConvertUnit(SerializedUnit unit)
        {
            return unit.Kind switch
            {
                UnitKind.InvokeMember => ConvertInvokeMember(unit),
                UnitKind.SetMember => ConvertSetMember(unit),
                UnitKind.Variable when unit.TypeName.Contains("SetVariable") => ConvertSetVariable(unit),
                UnitKind.Variable when unit.TypeName.Contains("SaveVariables") => new IRExpressionStatement
                {
                    Expression = new IRMethodCall
                    {
                        MethodName = "Save", IsStatic = true,
                        DeclaringType = new IRTypeRef { ShortName = "SavedVariables", FullName = "Unity.VisualScripting.SavedVariables" },
                    },
                },
                UnitKind.ControlFlow => ConvertControlFlow(unit),
                UnitKind.TriggerCustomEvent => ConvertTriggerCustomEvent(unit),
                UnitKind.Time => ConvertTime(unit),
                _ => null,
            };
        }

        IRStatement ConvertInvokeMember(SerializedUnit unit)
        {
            if (unit.Member == null) return null;

            var call = new IRMethodCall
            {
                MethodName = unit.Member.Name,
                DeclaringType = IRTypeRef.FromName(unit.Member.TargetTypeName),
                IsStatic = !unit.Member.RequiresTarget,
            };

            if (unit.Member.RequiresTarget)
                call.Target = ResolveValueInput(unit.Id, "target");

            foreach (var kv in unit.DefaultValues)
            {
                if (!kv.Key.StartsWith("%")) continue;
                call.Arguments.Add(ResolveValueInput(unit.Id, kv.Key));
            }

            return new IRExpressionStatement { Expression = call };
        }

        IRStatement ConvertSetMember(SerializedUnit unit)
        {
            if (unit.Member == null) return null;
            var target = ResolveValueInput(unit.Id, "target");
            var value = ResolveValueInput(unit.Id, "input");

            return new IRAssignment
            {
                Target = new IRMemberAccess { Target = target, MemberName = unit.Member.Name },
                Value = value,
            };
        }

        IRStatement ConvertSetVariable(SerializedUnit unit)
        {
            var name = ResolveValueInput(unit.Id, "name");
            var varName = name is IRLiteral { Value: string s } ? s : "unknown";
            var value = ResolveValueInput(unit.Id, "input");

            return new IRAssignment
            {
                Target = new IRIdentifier { Name = varName },
                Value = value,
            };
        }

        IRStatement ConvertControlFlow(SerializedUnit unit)
        {
            var shortName = unit.TypeName.Split('.').Last();

            if (shortName is "If" or "Branch")
            {
                var cond = ResolveValueInput(unit.Id, "condition");
                return new IRIf
                {
                    Condition = cond,
                    ThenBody = TraceControlFlow(unit.Id, "ifTrue"),
                    ElseBody = TraceControlFlow(unit.Id, "ifFalse"),
                };
            }

            if (shortName == "Sequence")
            {
                var block = new IRBlock();
                for (var i = 0; i < 10; i++)
                {
                    var sub = TraceControlFlow(unit.Id, i.ToString());
                    if (sub.Statements.Count == 0) break;
                    foreach (var s in sub.Statements) block.Statements.Add(s);
                }
                return block;
            }

            if (shortName == "For")
            {
                return new IRFor
                {
                    IndexVariable = "i",
                    First = ResolveValueInput(unit.Id, "firstIndex"),
                    Last = ResolveValueInput(unit.Id, "lastIndex"),
                    Step = ResolveValueInput(unit.Id, "step"),
                    Body = TraceControlFlow(unit.Id, "body"),
                };
            }

            if (shortName == "ForEach")
            {
                return new IRForEach
                {
                    ItemVariable = "item",
                    Collection = ResolveValueInput(unit.Id, "collection"),
                    Body = TraceControlFlow(unit.Id, "body"),
                };
            }

            if (shortName == "While")
            {
                return new IRWhile
                {
                    Condition = ResolveValueInput(unit.Id, "condition"),
                    Body = TraceControlFlow(unit.Id, "body"),
                };
            }

            if (shortName == "Break") return new IRBreak();

            return null;
        }

        IRStatement ConvertTriggerCustomEvent(SerializedUnit unit)
        {
            var eventName = ResolveValueInput(unit.Id, "name");
            var target = ResolveValueInput(unit.Id, "target");

            return new IRExpressionStatement
            {
                Expression = new IRMethodCall
                {
                    MethodName = "Trigger",
                    DeclaringType = new IRTypeRef { FullName = "Unity.VisualScripting.CustomEvent", ShortName = "CustomEvent" },
                    IsStatic = true,
                    Arguments = { eventName, target },
                },
            };
        }

        IRStatement ConvertTime(SerializedUnit unit)
        {
            if (unit.TypeName.Contains("WaitForSeconds"))
            {
                return new IRYieldReturn
                {
                    Expression = new IRConstructorCall
                    {
                        Type = new IRTypeRef { ShortName = "WaitForSeconds", FullName = "UnityEngine.WaitForSeconds" },
                        Arguments = { ResolveValueInput(unit.Id, "seconds") },
                    },
                };
            }

            return new IRExpressionStatement
            {
                Expression = new IRIdentifier { Name = $"/* {unit.TypeName.Split('.').Last()}: complex timer */" },
            };
        }

        IRExpression ResolveValueInput(string unitId, string portKey)
        {
            var edgeKey = $"{unitId}:{portKey}";
            if (_valueEdgesByDest.TryGetValue(edgeKey, out var edges) && edges.Count > 0)
            {
                var edge = edges[0];
                if (_snapshot.Units.TryGetValue(edge.SourceUnitId, out var srcUnit))
                    return ResolveValueOutput(srcUnit, edge.SourceKey);
            }

            // 接続がない場合は defaultValues から
            if (_snapshot.Units.TryGetValue(unitId, out var unit))
            {
                if (unit.DefaultValues.TryGetValue(portKey, out var val))
                {
                    if (val == null) return new IRNull();
                    if (val is string s) return new IRLiteral { Value = s, Type = IRTypeRef.String };
                    if (val is bool b) return new IRLiteral { Value = b, Type = IRTypeRef.Bool };
                    if (val is float f) return new IRLiteral { Value = f, Type = IRTypeRef.Float };
                    return new IRLiteral { Value = val, Type = IRTypeRef.Object };
                }
            }

            return new IRNull();
        }

        IRExpression ResolveValueOutput(SerializedUnit unit, string portKey)
        {
            switch (unit.Kind)
            {
                case UnitKind.Literal:
                {
                    // This ノード
                    if (unit.TypeName.Contains("This")) return new IRThis();

                    var litType = unit.LiteralValue != null
                        ? IRTypeRef.FromType(unit.LiteralValue.GetType())
                        : (unit.LiteralType != null ? IRTypeRef.FromName(unit.LiteralType) : IRTypeRef.Object);
                    return new IRLiteral { Value = unit.LiteralValue, Type = litType };
                }

                case UnitKind.Variable:
                {
                    var nameExpr = ResolveValueInput(unit.Id, "name");
                    var varName = nameExpr is IRLiteral { Value: string s } ? s : "var";
                    return new IRIdentifier { Name = varName };
                }

                case UnitKind.GetMember:
                {
                    var target = ResolveValueInput(unit.Id, "target");
                    var memberName = unit.Member?.Name ?? portKey;
                    return new IRMemberAccess { Target = target, MemberName = memberName };
                }

                case UnitKind.InvokeMember:
                {
                    var call = new IRMethodCall
                    {
                        MethodName = unit.Member?.Name ?? "unknown",
                        DeclaringType = IRTypeRef.FromName(unit.Member?.TargetTypeName),
                    };
                    if (unit.Member?.RequiresTarget == true)
                        call.Target = ResolveValueInput(unit.Id, "target");
                    foreach (var kv in unit.DefaultValues)
                    {
                        if (!kv.Key.StartsWith("%")) continue;
                        call.Arguments.Add(ResolveValueInput(unit.Id, kv.Key));
                    }
                    return call;
                }

                case UnitKind.Math:
                    return ResolveMath(unit, portKey);

                case UnitKind.Logic:
                case UnitKind.Comparison:
                    return ResolveLogic(unit);

                case UnitKind.Null:
                    return new IRNull();

                case UnitKind.CreateStruct:
                    return new IRConstructorCall { Type = IRTypeRef.FromName(unit.Member?.TargetTypeName) };

                case UnitKind.Expose:
                    return new IRMemberAccess
                    {
                        Target = ResolveValueInput(unit.Id, "input"),
                        MemberName = portKey,
                    };

                default:
                    return new IRIdentifier { Name = unit.TypeName.Split('.').Last() };
            }
        }

        IRExpression ResolveMath(SerializedUnit unit, string portKey)
        {
            var name = unit.TypeName.Split('.').Last();

            // ポートキーは Unit ごとに異なる:
            // Add: a/b/sum, Subtract: minuend/subtrahend/difference
            // Multiply: a/b/product, Divide: dividend/divisor/quotient
            if (name.Contains("Add")) return MathBinAuto(unit, BinaryOperator.Add, "a", "b");
            if (name.Contains("Subtract")) return MathBinAuto(unit, BinaryOperator.Subtract, "minuend", "subtrahend");
            if (name.Contains("Multiply")) return MathBinAuto(unit, BinaryOperator.Multiply, "a", "b");
            if (name.Contains("Divide")) return MathBinAuto(unit, BinaryOperator.Divide, "dividend", "divisor");
            if (name.Contains("Modulo")) return MathBinAuto(unit, BinaryOperator.Modulo, "dividend", "divisor");
            if (name.Contains("Sum")) return MathBinAuto(unit, BinaryOperator.Add, "a", "b");
            if (name.Contains("Minimum")) return MathfCallAuto("Min", unit);
            if (name.Contains("Maximum")) return MathfCallAuto("Max", unit);
            if (name.Contains("Lerp"))
                return new IRMethodCall
                {
                    MethodName = "Lerp", IsStatic = true,
                    DeclaringType = new IRTypeRef { ShortName = "Mathf", FullName = "UnityEngine.Mathf" },
                    Arguments = { ResolveValueInput(unit.Id, "a"), ResolveValueInput(unit.Id, "b"), ResolveValueInput(unit.Id, "t") },
                };

            return new IRIdentifier { Name = name };
        }

        IRMethodCall MathfCallAuto(string method, SerializedUnit unit) =>
            new()
            {
                MethodName = method, IsStatic = true,
                DeclaringType = new IRTypeRef { ShortName = "Mathf", FullName = "UnityEngine.Mathf" },
                Arguments = { FindInput(unit, "a"), FindInput(unit, "b") },
            };

        /// <summary>
        /// 値接続が存在するポートキーを優先して解決する。
        /// ポートキー名が Unit ごとに異なるため、接続データから実際のキーを探す。
        /// </summary>
        IRExpression FindInput(SerializedUnit unit, params string[] candidateKeys)
        {
            // 接続データから実際に存在するキーを探す
            foreach (var edge in _snapshot.ValueEdges)
            {
                if (edge.DestUnitId != unit.Id) continue;
                foreach (var k in candidateKeys)
                    if (edge.DestKey == k) return ResolveValueInput(unit.Id, k);
                // candidateKeys に含まれないキーでも使う
                return ResolveValueInput(unit.Id, edge.DestKey);
            }
            // 接続がなければ defaultValues から
            foreach (var k in candidateKeys)
            {
                if (unit.DefaultValues.ContainsKey(k))
                    return ResolveValueInput(unit.Id, k);
            }
            return new IRNull();
        }

        IRExpression MathBinAuto(SerializedUnit unit, BinaryOperator op, string leftKey, string rightKey)
        {
            // 接続データから実際のポートキーを探す
            var leftKeys = new List<string> { leftKey, "a" };
            var rightKeys = new List<string> { rightKey, "b" };

            IRExpression left = null, right = null;
            foreach (var edge in _snapshot.ValueEdges)
            {
                if (edge.DestUnitId != unit.Id) continue;
                if (leftKeys.Contains(edge.DestKey)) left = ResolveValueInput(unit.Id, edge.DestKey);
                else if (rightKeys.Contains(edge.DestKey)) right = ResolveValueInput(unit.Id, edge.DestKey);
            }

            left ??= ResolveValueInput(unit.Id, leftKey);
            right ??= ResolveValueInput(unit.Id, rightKey);

            return new IRBinaryOp { Left = left, Right = right, Operator = op };
        }

        IRExpression MathBin(SerializedUnit unit, BinaryOperator op)
        {
            return new IRBinaryOp
            {
                Left = ResolveValueInput(unit.Id, "a"),
                Right = ResolveValueInput(unit.Id, "b"),
                Operator = op,
            };
        }

        IRExpression ResolveLogic(SerializedUnit unit)
        {
            var name = unit.TypeName.Split('.').Last();
            BinaryOperator op = name switch
            {
                "And" => BinaryOperator.And,
                "Or" => BinaryOperator.Or,
                "Equal" => BinaryOperator.Equal,
                "NotEqual" => BinaryOperator.NotEqual,
                "Greater" => BinaryOperator.Greater,
                "GreaterOrEqual" => BinaryOperator.GreaterOrEqual,
                "Less" => BinaryOperator.Less,
                "LessOrEqual" => BinaryOperator.LessOrEqual,
                _ => BinaryOperator.Equal,
            };

            if (name == "Negate")
                return new IRUnaryOp
                {
                    Operand = ResolveValueInput(unit.Id, "input"),
                    Operator = UnaryOperator.LogicalNot,
                };

            return new IRBinaryOp
            {
                Left = ResolveValueInput(unit.Id, "a"),
                Right = ResolveValueInput(unit.Id, "b"),
                Operator = op,
            };
        }

        string GetContinuationKey(SerializedUnit unit)
        {
            var name = unit.TypeName.Split('.').Last();

            return name switch
            {
                "If" or "Branch" or "Switch" or "SwitchOnInteger" or "SwitchOnString" or "SwitchOnEnum"
                    or "ToggleFlow" or "TryCatch" => null,
                "For" or "ForEach" or "While" => "exit",
                "Once" => "after",
                _ => "exit",
            };
        }

        static string GetMethodName(SerializedUnit unit)
        {
            var name = unit.TypeName.Split('.').Last();

            // CustomEvent: defaultValues の name からメソッド名を生成
            if (unit.Kind == UnitKind.CustomEvent)
            {
                if (unit.DefaultValues.TryGetValue("name", out var eventName) && eventName is string s)
                    return "On" + s.Replace(" ", "");
                return "OnCustomEvent";
            }

            // InputSystem 系
            if (name.Contains("OnInputSystem")) return name;

            return name;
        }

        static string SanitizeClassName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "GeneratedScript";
            var sanitized = name.Replace(" ", "").Replace("-", "_");
            if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
            return sanitized;
        }
    }
}
