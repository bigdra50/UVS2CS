using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UVS2CS.GraphToIR;
using UVS2CS.IRToCSharp;

namespace UVS2CS.UI
{
    public static class BatchConverter
    {
        [MenuItem("Tools/UVS2CS/Convert All Sample Graphs")]
        static void ConvertAllSampleGraphs()
        {
            var outputDir = "Assets/Plugins/UVS2CS/Tests/Editor/RoundTrip/GeneratedOutput";
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
            var reader = new GraphReader();
            var successCount = 0;
            var failCount = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(path);
                if (asset == null) continue;

                try
                {
                    var ir = reader.Read(asset, path);
                    var code = CSharpEmitter.Emit(ir);

                    var outputPath = Path.Combine(outputDir, $"{ir.ClassName}.cs.txt");
                    File.WriteAllText(outputPath, code);

                    Debug.Log($"[UVS2CS] Converted: {asset.name} -> {outputPath}");
                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[UVS2CS] Failed: {asset.name} - {e.Message}");
                    failCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UVS2CS] Batch conversion complete: {successCount} succeeded, {failCount} failed");
        }
    }
}
