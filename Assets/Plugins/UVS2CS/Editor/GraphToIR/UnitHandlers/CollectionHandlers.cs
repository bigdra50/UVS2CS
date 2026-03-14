using System.Linq;
using Unity.VisualScripting;
using UVS2CS.IR;

namespace UVS2CS.GraphToIR.UnitHandlers
{
    public sealed class CollectionHandlers : IUnitHandler
    {
        public bool CanHandle(IUnit unit) =>
            unit is CreateList or AddListItem or RemoveListItem or RemoveListItemAt
            or GetListItem or SetListItem or ClearList or InsertListItem
            or ListContainsItem or MergeLists
            or CreateDictionary or AddDictionaryItem or RemoveDictionaryItem
            or GetDictionaryItem or SetDictionaryItem or ClearDictionary
            or DictionaryContainsKey or MergeDictionaries
            or CountItems or FirstItem or LastItem;

        public IRStatement HandleControlFlow(IUnit unit, FlowTracer tracer, ValueResolver resolver)
        {
            switch (unit)
            {
                case AddListItem:
                    return ListMethod(unit, "Add", "item", resolver);
                case InsertListItem:
                    return ListMethod2(unit, "Insert", "index", "item", resolver);
                case RemoveListItem:
                    return ListMethod(unit, "Remove", "item", resolver);
                case RemoveListItemAt:
                    return ListMethod(unit, "RemoveAt", "index", resolver);
                case SetListItem:
                {
                    var list = resolver.Resolve(unit.valueInputs["list"]);
                    var index = resolver.Resolve(unit.valueInputs["index"]);
                    var item = resolver.Resolve(unit.valueInputs["item"]);
                    return new IRAssignment
                    {
                        Target = new IRIndexAccess { Target = list, Index = index },
                        Value = item,
                    };
                }
                case ClearList:
                    return ListMethodNoArg(unit, "Clear", resolver);
                case AddDictionaryItem:
                    return DictMethod2(unit, "Add", "key", "value", resolver);
                case RemoveDictionaryItem:
                    return DictMethod(unit, "Remove", "key", resolver);
                case SetDictionaryItem:
                {
                    var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
                    var key = resolver.Resolve(unit.valueInputs["key"]);
                    var value = resolver.Resolve(unit.valueInputs["value"]);
                    return new IRAssignment
                    {
                        Target = new IRIndexAccess { Target = dict, Index = key },
                        Value = value,
                    };
                }
                case ClearDictionary:
                    return DictMethodNoArg(unit, "Clear", resolver);
                default:
                    return null;
            }
        }

        public IRExpression HandleValue(IUnit unit, ValueOutput port, ValueResolver resolver)
        {
            switch (unit)
            {
                case CreateList:
                    return new IRConstructorCall
                    {
                        Type = new IRTypeRef { FullName = "System.Collections.Generic.List`1", ShortName = "List<object>", Namespace = "System.Collections.Generic" },
                    };
                case CreateDictionary:
                    return new IRConstructorCall
                    {
                        Type = new IRTypeRef { FullName = "System.Collections.Generic.Dictionary`2", ShortName = "Dictionary<object, object>", Namespace = "System.Collections.Generic" },
                    };
                case GetListItem:
                {
                    var list = resolver.Resolve(unit.valueInputs["list"]);
                    var index = resolver.Resolve(unit.valueInputs["index"]);
                    return new IRIndexAccess { Target = list, Index = index };
                }
                case GetDictionaryItem:
                {
                    var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
                    var key = resolver.Resolve(unit.valueInputs["key"]);
                    return new IRIndexAccess { Target = dict, Index = key };
                }
                case CountItems:
                {
                    var coll = resolver.Resolve(unit.valueInputs["collection"]);
                    return new IRMemberAccess { Target = coll, MemberName = "Count" };
                }
                case FirstItem:
                {
                    var list = resolver.Resolve(unit.valueInputs["list"]);
                    return new IRIndexAccess { Target = list, Index = new IRLiteral { Value = 0, Type = IRTypeRef.Int } };
                }
                case LastItem:
                {
                    var list = resolver.Resolve(unit.valueInputs["list"]);
                    return new IRIndexAccess
                    {
                        Target = list,
                        Index = new IRBinaryOp
                        {
                            Left = new IRMemberAccess { Target = list, MemberName = "Count" },
                            Right = new IRLiteral { Value = 1, Type = IRTypeRef.Int },
                            Operator = IR.BinaryOperator.Subtract,
                        },
                    };
                }
                case ListContainsItem:
                {
                    var list = resolver.Resolve(unit.valueInputs["list"]);
                    var item = resolver.Resolve(unit.valueInputs["item"]);
                    return new IRMethodCall
                    {
                        Target = list,
                        MethodName = "Contains",
                        Arguments = { item },
                    };
                }
                case DictionaryContainsKey:
                {
                    var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
                    var key = resolver.Resolve(unit.valueInputs["key"]);
                    return new IRMethodCall
                    {
                        Target = dict,
                        MethodName = "ContainsKey",
                        Arguments = { key },
                    };
                }
                default:
                    return new IRNull();
            }
        }

        static IRExpressionStatement ListMethod(IUnit unit, string method, string argKey, ValueResolver resolver)
        {
            var list = resolver.Resolve(unit.valueInputs["list"]);
            var arg = resolver.Resolve(unit.valueInputs[argKey]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = list, MethodName = method, Arguments = { arg } },
            };
        }

        static IRExpressionStatement ListMethod2(IUnit unit, string method, string key1, string key2, ValueResolver resolver)
        {
            var list = resolver.Resolve(unit.valueInputs["list"]);
            var arg1 = resolver.Resolve(unit.valueInputs[key1]);
            var arg2 = resolver.Resolve(unit.valueInputs[key2]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = list, MethodName = method, Arguments = { arg1, arg2 } },
            };
        }

        static IRExpressionStatement ListMethodNoArg(IUnit unit, string method, ValueResolver resolver)
        {
            var list = resolver.Resolve(unit.valueInputs["list"]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = list, MethodName = method },
            };
        }

        static IRExpressionStatement DictMethod(IUnit unit, string method, string argKey, ValueResolver resolver)
        {
            var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
            var arg = resolver.Resolve(unit.valueInputs[argKey]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = dict, MethodName = method, Arguments = { arg } },
            };
        }

        static IRExpressionStatement DictMethod2(IUnit unit, string method, string key1, string key2, ValueResolver resolver)
        {
            var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
            var arg1 = resolver.Resolve(unit.valueInputs[key1]);
            var arg2 = resolver.Resolve(unit.valueInputs[key2]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = dict, MethodName = method, Arguments = { arg1, arg2 } },
            };
        }

        static IRExpressionStatement DictMethodNoArg(IUnit unit, string method, ValueResolver resolver)
        {
            var dict = resolver.Resolve(unit.valueInputs["dictionary"]);
            return new IRExpressionStatement
            {
                Expression = new IRMethodCall { Target = dict, MethodName = method },
            };
        }
    }
}
