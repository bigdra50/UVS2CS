using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UVS2CS.GraphToIR
{
    /// <summary>
    /// ScriptGraphAsset の _data._json を直接パースし、
    /// Define() 失敗した Unit の member 情報と接続情報を補完するデータソース。
    /// </summary>
    public sealed class AssetJsonReader
    {
        public Dictionary<string, RawUnit> Units { get; } = new();
        public List<RawConnection> ControlConnections { get; } = new();
        public List<RawConnection> ValueConnections { get; } = new();

        public static AssetJsonReader FromAssetPath(string assetPath)
        {
            var reader = new AssetJsonReader();
            var fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!File.Exists(fullPath)) return reader;

            var text = File.ReadAllText(fullPath);
            var jsonMatch = Regex.Match(text, @"_json:\s*'(.+)'", RegexOptions.Singleline);
            if (!jsonMatch.Success) return reader;

            var json = jsonMatch.Groups[1].Value;
            reader.ParseElements(json);
            return reader;
        }

        public static AssetJsonReader FromJson(string json)
        {
            var reader = new AssetJsonReader();
            reader.ParseElements(json);
            return reader;
        }

        void ParseElements(string json)
        {
            // elements 配列内の各オブジェクトを走査
            var elementsMatch = Regex.Match(json, @"""elements"":\[(.+)\]", RegexOptions.Singleline);
            if (!elementsMatch.Success) return;

            var elements = elementsMatch.Groups[1].Value;

            // 各要素をパース（$type で型判別）
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
                        ParseElement(obj);
                        start = -1;
                    }
                }
            }
        }

        void ParseElement(string obj)
        {
            var typeMatch = Regex.Match(obj, @"""\$type"":""([^""]+)""");
            if (!typeMatch.Success) return;

            var typeName = typeMatch.Groups[1].Value;

            if (typeName == "Unity.VisualScripting.ControlConnection")
            {
                ParseConnection(obj, ControlConnections);
                return;
            }

            if (typeName == "Unity.VisualScripting.ValueConnection")
            {
                ParseConnection(obj, ValueConnections);
                return;
            }

            if (typeName == "Unity.VisualScripting.GraphGroup" || typeName == "Unity.VisualScripting.StickyNote")
                return;

            // Unit
            var unit = new RawUnit { TypeName = typeName };

            var idMatch = Regex.Match(obj, @"""\$id"":""([^""]+)""");
            if (idMatch.Success) unit.Id = idMatch.Groups[1].Value;

            // member 情報
            var memberMatch = Regex.Match(obj, @"""member"":\{([^}]+)\}");
            if (memberMatch.Success)
            {
                var m = memberMatch.Groups[1].Value;
                unit.MemberName = ExtractString(m, "name");
                unit.MemberTargetType = ExtractString(m, "targetTypeName") ?? ExtractString(m, "targetType");

                var paramMatch = Regex.Match(m, @"""parameterTypes"":\[([^\]]*)\]");
                if (paramMatch.Success)
                {
                    var paramStr = paramMatch.Groups[1].Value;
                    var paramTypes = new List<string>();
                    foreach (Match pm in Regex.Matches(paramStr, @"""([^""]+)"""))
                        paramTypes.Add(pm.Groups[1].Value);
                    unit.MemberParameterTypes = paramTypes;
                }
            }

            // defaultValues
            var defaultsMatch = Regex.Match(obj, @"""defaultValues"":\{([^}]*(?:\{[^}]*\}[^}]*)*)\}");
            if (defaultsMatch.Success)
            {
                var defaults = defaultsMatch.Groups[1].Value;
                foreach (Match dm in Regex.Matches(defaults, @"""([^""]+)"":\{?""\$content"":([^,}]+)"))
                {
                    unit.DefaultValues[dm.Groups[1].Value] = dm.Groups[2].Value.Trim('"');
                }
                // Simple string values
                foreach (Match dm in Regex.Matches(defaults, @"""([^""]+)"":\{""\$content"":""([^""]*)"","))
                {
                    unit.DefaultValues[dm.Groups[1].Value] = dm.Groups[2].Value;
                }
            }

            if (!string.IsNullOrEmpty(unit.Id))
                Units[unit.Id] = unit;
        }

        void ParseConnection(string obj, List<RawConnection> list)
        {
            var conn = new RawConnection
            {
                SourceId = ExtractRef(obj, "sourceUnit"),
                SourceKey = ExtractString(obj, "sourceKey"),
                DestId = ExtractRef(obj, "destinationUnit"),
                DestKey = ExtractString(obj, "destinationKey"),
            };

            if (conn.SourceId != null && conn.DestId != null)
                list.Add(conn);
        }

        static string ExtractString(string json, string key)
        {
            var match = Regex.Match(json, $@"""{key}"":""([^""]+)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        static string ExtractRef(string json, string key)
        {
            var match = Regex.Match(json, $@"""{key}"":\{{""\$ref"":""([^""]+)""\}}");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public sealed class RawUnit
    {
        public string Id { get; set; }
        public string TypeName { get; set; }
        public string MemberName { get; set; }
        public string MemberTargetType { get; set; }
        public List<string> MemberParameterTypes { get; set; }
        public Dictionary<string, string> DefaultValues { get; } = new();
    }

    public sealed class RawConnection
    {
        public string SourceId { get; set; }
        public string SourceKey { get; set; }
        public string DestId { get; set; }
        public string DestKey { get; set; }
    }
}
