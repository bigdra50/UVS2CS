using System;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UVS2CS.CSharpToIR;
using UVS2CS.GraphToIR;
using UVS2CS.IRToCSharp;
using UVS2CS.IRToGraph;

namespace UVS2CS.UI
{
    public sealed class UVS2CSWindow : EditorWindow
    {
        ScriptGraphAsset _sourceGraph;
        TextAsset _sourceScript;
        string _outputPreview = "";
        Vector2 _scrollPos;
        int _tabIndex;

        [MenuItem("Tools/UVS2CS Converter")]
        static void Open()
        {
            GetWindow<UVS2CSWindow>("UVS2CS");
        }

        void OnGUI()
        {
            _tabIndex = GUILayout.Toolbar(_tabIndex, new[] { "Graph → C#", "C# → Graph" });

            EditorGUILayout.Space(8);

            switch (_tabIndex)
            {
                case 0:
                    DrawGraphToCSharp();
                    break;
                case 1:
                    DrawCSharpToGraph();
                    break;
            }
        }

        void DrawGraphToCSharp()
        {
            _sourceGraph = (ScriptGraphAsset)EditorGUILayout.ObjectField(
                "Script Graph", _sourceGraph, typeof(ScriptGraphAsset), false);

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledGroupScope(_sourceGraph == null))
            {
                if (GUILayout.Button("Convert to C#", GUILayout.Height(30)))
                    ConvertGraphToCSharp();
            }

            if (!string.IsNullOrEmpty(_outputPreview))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Output Preview:");

                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
                EditorGUILayout.TextArea(_outputPreview, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy to Clipboard"))
                        EditorGUIUtility.systemCopyBuffer = _outputPreview;

                    if (GUILayout.Button("Save as .cs"))
                        SaveCSharpFile();
                }
            }
        }

        void DrawCSharpToGraph()
        {
            _sourceScript = (TextAsset)EditorGUILayout.ObjectField(
                "C# Script", _sourceScript, typeof(TextAsset), false);

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledGroupScope(_sourceScript == null))
            {
                if (GUILayout.Button("Convert to Graph", GUILayout.Height(30)))
                    ConvertCSharpToGraph();
            }
        }

        void ConvertGraphToCSharp()
        {
            try
            {
                var reader = new GraphReader();
                var ir = reader.Read(_sourceGraph);
                _outputPreview = CSharpEmitter.Emit(ir);
            }
            catch (Exception e)
            {
                _outputPreview = $"// Error: {e.Message}\n// {e.StackTrace}";
                Debug.LogError($"[UVS2CS] Conversion failed: {e}");
            }
        }

        void ConvertCSharpToGraph()
        {
            try
            {
                var parser = new CSharpParser();
                var ir = parser.Parse(_sourceScript.text);

                var writer = new GraphWriter();
                var asset = writer.WriteAsset(ir);

                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Script Graph", ir.ClassName, "asset",
                    "Save the generated Script Graph asset");

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(asset, path);
                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(asset);
                    Debug.Log($"[UVS2CS] Graph saved to {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UVS2CS] Conversion failed: {e}");
            }
        }

        void SaveCSharpFile()
        {
            var defaultName = _sourceGraph != null ? _sourceGraph.name : "GeneratedScript";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save C# Script", defaultName, "cs",
                "Save the generated C# script");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, _outputPreview);
                AssetDatabase.Refresh();
                Debug.Log($"[UVS2CS] Script saved to {path}");
            }
        }
    }
}
