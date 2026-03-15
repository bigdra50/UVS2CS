using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace UVS2CS.GraphToIR.Serialized
{
    /// <summary>
    /// ScriptGraphAsset の SerializedObject から _data._json を取得し、
    /// SerializedGraphSnapshot を構築する。
    /// </summary>
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

            ParseVariables(json, snapshot);
            ParseElements(json, snapshot);

            return snapshot;
        }

        static void ParseVariables(string json, SerializedGraphSnapshot snapshot)
        {
            // "variables":{"collection":{"$content":[{"name":"...","value":...}]}}
            var varMatch = Regex.Match(json, @"""variables"":\{""collection"":\{""\$content"":\[(.+?)\]", RegexOptions.Singleline);
            if (!varMatch.Success) return;

            foreach (Match m in Regex.Matches(varMatch.Groups[1].Value, @"\{""name"":""([^""]+)"""))
                snapshot.Variables[m.Groups[1].Value] = null;
        }

        static void ParseElements(string json, SerializedGraphSnapshot snapshot)
        {
            var elemMatch = Regex.Match(json, @"""elements"":\[(.+)\],""\$version", RegexOptions.Singleline);
            if (!elemMatch.Success)
            {
                elemMatch = Regex.Match(json, @"""elements"":\[(.+)\]}", RegexOptions.Singleline);
                if (!elemMatch.Success) return;
            }

            var elements = elemMatch.Groups[1].Value;

            var depth = 0;
            var start = -1;

            for (var i = 0; i < elements.Length; i++)
            {
                if (elements[i] == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (elements[i] == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        var obj = elements.Substring(start, i - start + 1);
                        ParseElement(obj, snapshot);
                        start = -1;
                    }
                }
            }
        }

        static void ParseElement(string obj, SerializedGraphSnapshot snapshot)
        {
            var typeName = ExtractString(obj, "$type");
            if (typeName == null) return;

            // 型名正規化: Bolt.* / Ludiq.* → Unity.VisualScripting.*
            typeName = NormalizeTypeName(typeName);

            if (typeName == "Unity.VisualScripting.ControlConnection")
            {
                snapshot.ControlEdges.Add(ParseEdge(obj));
                return;
            }
            if (typeName == "Unity.VisualScripting.ValueConnection")
            {
                snapshot.ValueEdges.Add(ParseEdge(obj));
                return;
            }
            if (typeName.Contains("GraphGroup") || typeName.Contains("StickyNote"))
                return;

            var unit = new SerializedUnit
            {
                TypeName = typeName,
                Kind = ClassifyUnit(typeName),
            };

            unit.Id = ExtractString(obj, "$id");

            // member 情報
            var memberMatch = Regex.Match(obj, @"""member"":\{([^}]+)\}");
            if (memberMatch.Success)
            {
                var m = memberMatch.Groups[1].Value;
                unit.Member = new SerializedMember
                {
                    Name = ExtractString(m, "name"),
                    TargetTypeName = ExtractString(m, "targetTypeName") ?? ExtractString(m, "targetType"),
                };

                var paramMatch = Regex.Match(m, @"""parameterTypes"":\[([^\]]*)\]");
                if (paramMatch.Success)
                {
                    foreach (Match pm in Regex.Matches(paramMatch.Groups[1].Value, @"""([^""]+)"""))
                        unit.Member.ParameterTypeNames.Add(pm.Groups[1].Value);
                }
            }

            // defaultValues
            ParseDefaultValues(obj, unit);

            // VariableKind
            var kindStr = ExtractString(obj, "kind");
            if (kindStr != null) unit.VariableKind = kindStr;

            // argumentCount
            var argCountMatch = Regex.Match(obj, @"""argumentCount"":(\d+)");
            if (argCountMatch.Success) unit.ArgumentCount = int.Parse(argCountMatch.Groups[1].Value);

            // coroutine
            if (obj.Contains(@"""coroutine"":true")) unit.Coroutine = true;

            // Literal value/type
            var literalTypeMatch = Regex.Match(obj, @"""type"":\{[^}]*""\$type"":""([^""]+)""");
            if (unit.Kind == UnitKind.Literal && literalTypeMatch.Success)
                unit.LiteralType = literalTypeMatch.Groups[1].Value;

            if (!string.IsNullOrEmpty(unit.Id))
                snapshot.Units[unit.Id] = unit;
        }

        static void ParseDefaultValues(string obj, SerializedUnit unit)
        {
            // defaultValues のキーと値を抽出
            var dvMatch = Regex.Match(obj, @"""defaultValues"":\{(.+?)\},""position""", RegexOptions.Singleline);
            if (!dvMatch.Success)
            {
                dvMatch = Regex.Match(obj, @"""defaultValues"":\{(.+?)\}", RegexOptions.Singleline);
                if (!dvMatch.Success) return;
            }

            var dvStr = dvMatch.Groups[1].Value;

            // "$content":"stringValue" パターン
            foreach (Match m in Regex.Matches(dvStr, @"""([^""]+)"":\{""\$content"":""([^""]*)"","))
                unit.DefaultValues[m.Groups[1].Value] = m.Groups[2].Value;

            // "$content":numericValue パターン
            foreach (Match m in Regex.Matches(dvStr, @"""([^""]+)"":\{""\$content"":([0-9.eE+-]+),"))
            {
                if (!unit.DefaultValues.ContainsKey(m.Groups[1].Value))
                {
                    if (float.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f))
                        unit.DefaultValues[m.Groups[1].Value] = f;
                }
            }

            // "$content":true/false パターン
            foreach (Match m in Regex.Matches(dvStr, @"""([^""]+)"":\{""\$content"":(true|false),"))
            {
                if (!unit.DefaultValues.ContainsKey(m.Groups[1].Value))
                    unit.DefaultValues[m.Groups[1].Value] = m.Groups[2].Value == "true";
            }

            // null パターン
            foreach (Match m in Regex.Matches(dvStr, @"""([^""]+)"":null"))
            {
                if (!unit.DefaultValues.ContainsKey(m.Groups[1].Value))
                    unit.DefaultValues[m.Groups[1].Value] = null;
            }
        }

        static SerializedEdge ParseEdge(string obj)
        {
            return new SerializedEdge
            {
                SourceUnitId = ExtractRef(obj, "sourceUnit"),
                SourceKey = ExtractString(obj, "sourceKey"),
                DestUnitId = ExtractRef(obj, "destinationUnit"),
                DestKey = ExtractString(obj, "destinationKey"),
            };
        }

        static UnitKind ClassifyUnit(string typeName)
        {
            if (typeName.Contains("InvokeMember")) return UnitKind.InvokeMember;
            if (typeName.Contains("GetMember")) return UnitKind.GetMember;
            if (typeName.Contains("SetMember")) return UnitKind.SetMember;
            if (typeName.Contains("GetVariable")) return UnitKind.Variable;
            if (typeName.Contains("SetVariable")) return UnitKind.Variable;
            if (typeName.Contains("IsVariableDefined")) return UnitKind.Variable;
            if (typeName.Contains("Literal")) return UnitKind.Literal;
            if (typeName.Contains("If") || typeName.Contains("For") || typeName.Contains("While")
                || typeName.Contains("Sequence") || typeName.Contains("Switch") || typeName.Contains("Break"))
                return UnitKind.ControlFlow;
            if (typeName.Contains("Start") || typeName.Contains("Update") || typeName.Contains("FixedUpdate")
                || typeName.Contains("OnEnable") || typeName.Contains("OnDisable") || typeName.Contains("OnDestroy")
                || typeName.Contains("OnTrigger") || typeName.Contains("OnCollision")
                || typeName.Contains("OnMouse") || typeName.Contains("OnInput") || typeName.Contains("Event"))
                return UnitKind.Event;
            if (typeName.Contains("CustomEvent")) return UnitKind.CustomEvent;
            if (typeName.Contains("TriggerCustomEvent")) return UnitKind.TriggerCustomEvent;
            if (typeName.Contains("Add") || typeName.Contains("Subtract") || typeName.Contains("Multiply")
                || typeName.Contains("Divide") || typeName.Contains("Sum") || typeName.Contains("Lerp"))
                return UnitKind.Math;
            if (typeName.Contains("And") || typeName.Contains("Or") || typeName.Contains("Negate"))
                return UnitKind.Logic;
            if (typeName.Contains("Equal") || typeName.Contains("Greater") || typeName.Contains("Less"))
                return UnitKind.Comparison;
            if (typeName.Contains("Null")) return UnitKind.Null;
            if (typeName.Contains("Timer") || typeName.Contains("Cooldown") || typeName.Contains("Wait"))
                return UnitKind.Time;
            if (typeName.Contains("List") || typeName.Contains("Dictionary") || typeName.Contains("Count"))
                return UnitKind.Collection;
            if (typeName.Contains("GraphInput") || typeName.Contains("GraphOutput") || typeName.Contains("Subgraph"))
                return UnitKind.Nesting;
            if (typeName.Contains("CreateStruct")) return UnitKind.CreateStruct;
            if (typeName.Contains("Expose")) return UnitKind.Expose;
            return UnitKind.Unknown;
        }

        static string NormalizeTypeName(string typeName)
        {
            // Bolt.* → Unity.VisualScripting.*
            if (typeName.StartsWith("Bolt."))
                return "Unity.VisualScripting." + typeName.Substring(5);
            if (typeName.StartsWith("Ludiq."))
                return "Unity.VisualScripting." + typeName.Substring(6);
            return typeName;
        }

        static string ExtractString(string json, string key)
        {
            var match = Regex.Match(json, $@"""{Regex.Escape(key)}"":""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        static string ExtractRef(string json, string key)
        {
            var match = Regex.Match(json, $@"""{Regex.Escape(key)}"":\{{""\$ref"":""([^""]+)""\}}");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
