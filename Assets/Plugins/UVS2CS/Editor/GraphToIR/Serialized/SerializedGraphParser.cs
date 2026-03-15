using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using UnityEditor;

namespace UVS2CS.GraphToIR.Serialized
{
    public static class SerializedGraphParser
    {
        public static SerializedGraphSnapshot Parse(ScriptGraphAsset asset)
        {
            var so = new SerializedObject(asset);
            var jsonProp = so.FindProperty("_data._json");
            if (jsonProp == null || string.IsNullOrEmpty(jsonProp.stringValue))
                return new SerializedGraphSnapshot();

            return ParseJson(jsonProp.stringValue);
        }

        public static SerializedGraphSnapshot ParseJson(string json)
        {
            var snapshot = new SerializedGraphSnapshot();

            JObject root;
            try { root = JObject.Parse(json); }
            catch { return snapshot; }

            var graph = root["graph"] as JObject ?? root;

            ParseVariables(graph, snapshot);
            ParseElements(graph, snapshot);

            return snapshot;
        }

        static void ParseVariables(JObject graph, SerializedGraphSnapshot snapshot)
        {
            var vars = graph.SelectToken("variables.collection.$content") as JArray;
            if (vars == null) return;

            foreach (var v in vars)
            {
                var name = v["name"]?.ToString();
                if (name == null) continue;

                object value = null;
                var valToken = v["value"];
                if (valToken != null && valToken.Type != JTokenType.Null)
                {
                    if (valToken is JObject valObj && valObj["$content"] != null)
                        value = ExtractContent(valObj);
                    else
                        value = ExtractPrimitive(valToken);
                }

                snapshot.Variables[name] = value;
            }
        }

        static void ParseElements(JObject graph, SerializedGraphSnapshot snapshot)
        {
            var elements = graph["elements"] as JArray;
            if (elements == null) return;

            // 2パス: 1パス目で Unit ($id 付き) を登録、2パス目で接続 ($ref 解決)
            foreach (var elem in elements)
            {
                if (elem is not JObject obj) continue;
                var typeName = obj["$type"]?.ToString();
                if (typeName == null) continue;

                typeName = NormalizeTypeName(typeName);

                if (typeName == "Unity.VisualScripting.ControlConnection")
                {
                    snapshot.ControlEdges.Add(ParseEdge(obj));
                    continue;
                }
                if (typeName == "Unity.VisualScripting.ValueConnection")
                {
                    snapshot.ValueEdges.Add(ParseEdge(obj));
                    continue;
                }
                if (typeName.Contains("GraphGroup") || typeName.Contains("StickyNote"))
                    continue;

                var unit = ParseUnit(obj, typeName);
                if (!string.IsNullOrEmpty(unit.Id))
                    snapshot.Units[unit.Id] = unit;
            }
        }

        static SerializedUnit ParseUnit(JObject obj, string typeName)
        {
            var unit = new SerializedUnit
            {
                Id = obj["$id"]?.ToString(),
                TypeName = typeName,
                Kind = ClassifyUnit(typeName),
            };

            // position
            var pos = obj["position"] as JObject;
            if (pos != null)
            {
                unit.PositionX = pos["x"]?.Value<float>() ?? 0;
                unit.PositionY = pos["y"]?.Value<float>() ?? 0;
            }

            // member
            var memberObj = obj["member"] as JObject;
            if (memberObj != null)
            {
                unit.Member = new SerializedMember
                {
                    Name = memberObj["name"]?.ToString(),
                    TargetTypeName = memberObj["targetTypeName"]?.ToString()
                        ?? memberObj["targetType"]?.ToString(),
                };

                var paramTypes = memberObj["parameterTypes"] as JArray;
                if (paramTypes != null)
                {
                    foreach (var p in paramTypes)
                        unit.Member.ParameterTypeNames.Add(p.ToString());
                }
            }

            // defaultValues
            var defaults = obj["defaultValues"] as JObject;
            if (defaults != null)
            {
                foreach (var kv in defaults)
                {
                    var val = kv.Value;
                    if (val == null || val.Type == JTokenType.Null)
                    {
                        unit.DefaultValues[kv.Key] = null;
                        continue;
                    }

                    if (val is JObject valObj && valObj["$content"] != null)
                        unit.DefaultValues[kv.Key] = ExtractContent(valObj);
                    else
                        unit.DefaultValues[kv.Key] = ExtractPrimitive(val);
                }
            }

            // kind (VariableKind)
            var kindStr = obj["kind"]?.ToString();
            if (kindStr != null) unit.VariableKind = kindStr;

            // specifyFallback
            var specifyFallback = obj["specifyFallback"];
            if (specifyFallback != null)
                unit.DefaultValues["_specifyFallback"] = specifyFallback.Value<bool>();

            // argumentCount
            var argCount = obj["argumentCount"];
            if (argCount != null) unit.ArgumentCount = argCount.Value<int>();

            // coroutine
            var coroutine = obj["coroutine"];
            if (coroutine != null) unit.Coroutine = coroutine.Value<bool>();

            // chainable
            var chainable = obj["chainable"];
            if (chainable != null)
                unit.DefaultValues["_chainable"] = chainable.Value<bool>();

            // branchCount (Sequence)
            var branchCount = obj["branchCount"];
            if (branchCount != null) unit.OutputCount = branchCount.Value<int>();

            // options (Switch)
            var options = obj["options"] as JArray;
            if (options != null) unit.OutputCount = options.Count;

            // Literal: type and value
            var typeToken = obj["type"];
            if (typeToken is JObject typeObj)
            {
                unit.LiteralType = typeObj["$type"]?.ToString();
            }
            var valueToken = obj["value"];
            if (valueToken != null)
            {
                if (valueToken is JObject valueObj && valueObj["$content"] != null)
                    unit.LiteralValue = ExtractContent(valueObj);
                else
                    unit.LiteralValue = ExtractPrimitive(valueToken);
            }

            return unit;
        }

        static SerializedEdge ParseEdge(JObject obj)
        {
            return new SerializedEdge
            {
                SourceUnitId = (obj["sourceUnit"] as JObject)?["$ref"]?.ToString(),
                SourceKey = obj["sourceKey"]?.ToString(),
                DestUnitId = (obj["destinationUnit"] as JObject)?["$ref"]?.ToString(),
                DestKey = obj["destinationKey"]?.ToString(),
            };
        }

        static object ExtractContent(JObject wrapper)
        {
            var content = wrapper["$content"];
            var type = wrapper["$type"]?.ToString();

            if (content == null || content.Type == JTokenType.Null) return null;

            return type switch
            {
                "System.String" => content.ToString(),
                "System.Int32" => content.Value<int>(),
                "System.Single" => content.Value<float>(),
                "System.Double" => content.Value<double>(),
                "System.Boolean" => content.Value<bool>(),
                "System.Int64" => content.Value<long>(),
                "UnityEngine.ForceMode" => content.Value<int>(),
                _ when type != null && type.StartsWith("UnityEngine.") =>
                    content.Type == JTokenType.Integer ? content.Value<int>() : (object)content.ToString(),
                _ => content.Type switch
                {
                    JTokenType.Integer => content.Value<int>(),
                    JTokenType.Float => content.Value<float>(),
                    JTokenType.Boolean => content.Value<bool>(),
                    JTokenType.String => content.ToString(),
                    _ => content.ToString(),
                },
            };
        }

        static object ExtractPrimitive(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Integer => token.Value<int>(),
                JTokenType.Float => token.Value<float>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.String => token.ToString(),
                JTokenType.Null => null,
                _ => token.ToString(),
            };
        }

        static UnitKind ClassifyUnit(string typeName)
        {
            var name = typeName.Split('.').Last();

            if (name == "InvokeMember") return UnitKind.InvokeMember;
            if (name == "GetMember") return UnitKind.GetMember;
            if (name == "SetMember") return UnitKind.SetMember;
            if (name == "GetVariable" || name == "SetVariable" || name == "IsVariableDefined"
                || name == "SaveVariables") return UnitKind.Variable;
            if (name == "Literal") return UnitKind.Literal;
            if (name == "This") return UnitKind.Literal;
            if (name == "Null") return UnitKind.Null;
            if (name == "NullCheck") return UnitKind.NullCheck;
            if (name == "NullCoalesce") return UnitKind.NullCheck;
            if (name == "CustomEvent") return UnitKind.CustomEvent;
            if (name == "TriggerCustomEvent") return UnitKind.TriggerCustomEvent;
            if (name == "CreateStruct") return UnitKind.CreateStruct;
            if (name == "Expose") return UnitKind.Expose;
            if (name == "Formula") return UnitKind.Formula;

            if (name is "If" or "For" or "ForEach" or "While" or "Sequence" or "Break"
                or "SwitchOnInteger" or "SwitchOnString" or "SwitchOnEnum"
                or "Once" or "Cache" or "ToggleFlow" or "ToggleValue"
                or "TryCatch" or "Throw" or "SelectOnFlow")
                return UnitKind.ControlFlow;

            if (name.Contains("Add") || name.Contains("Subtract") || name.Contains("Multiply")
                || name.Contains("Divide") || name.Contains("Modulo") || name.Contains("Sum")
                || name.Contains("Lerp") || name.Contains("MoveTowards") || name.Contains("Minimum")
                || name.Contains("Maximum") || name.Contains("Absolute") || name.Contains("Normalize")
                || name.Contains("Distance") || name.Contains("Angle") || name.Contains("DotProduct")
                || name.Contains("CrossProduct") || name.Contains("Average") || name.Contains("Round")
                || name.Contains("Root") || name.Contains("Exponentiate") || name.Contains("PerSecond")
                || name.Contains("Project"))
                return UnitKind.Math;

            if (name is "And" or "Or" or "Negate" or "ExclusiveOr")
                return UnitKind.Logic;
            if (name is "Equal" or "NotEqual" or "Greater" or "GreaterOrEqual"
                or "Less" or "LessOrEqual" or "Comparison" or "ApproximatelyEqual"
                or "NotApproximatelyEqual")
                return UnitKind.Comparison;

            if (name.Contains("Timer") || name.Contains("Cooldown") || name.Contains("Wait"))
                return UnitKind.Time;
            if (name.Contains("List") || name.Contains("Dictionary") || name.Contains("Count")
                || name.Contains("FirstItem") || name.Contains("LastItem"))
                return UnitKind.Collection;
            if (name.Contains("GraphInput") || name.Contains("GraphOutput") || name.Contains("Subgraph"))
                return UnitKind.Nesting;

            // Event: 残りの既知のイベント名
            if (name.Contains("Start") || name.Contains("Update") || name.Contains("FixedUpdate")
                || name.Contains("LateUpdate") || name.Contains("OnEnable") || name.Contains("OnDisable")
                || name.Contains("OnDestroy") || name.Contains("OnTrigger") || name.Contains("OnCollision")
                || name.Contains("OnMouse") || name.Contains("OnApplication") || name.Contains("OnBecame")
                || name.Contains("OnAnimator") || name.Contains("OnGUI") || name.Contains("OnTransform")
                || name.Contains("OnInput") || name.Contains("OnButton") || name.Contains("OnKey"))
                return UnitKind.Event;

            return UnitKind.Unknown;
        }

        static string NormalizeTypeName(string typeName)
        {
            if (typeName.StartsWith("Bolt."))
                return "Unity.VisualScripting." + typeName.Substring(5);
            if (typeName.StartsWith("Ludiq."))
                return "Unity.VisualScripting." + typeName.Substring(6);
            return typeName;
        }
    }
}
