using System.Linq;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UVS2CS.CSharpToIR;
using UVS2CS.GraphToIR;
using UVS2CS.IR;
using UVS2CS.IRToCSharp;
using UVS2CS.IRToGraph;

namespace UVS2CS.Tests.RoundTrip
{
    public class SampleGraphConversionTests
    {
        static ScriptGraphAsset LoadGraph(string searchName)
        {
            var guids = AssetDatabase.FindAssets($"t:ScriptGraphAsset {searchName}");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"ScriptGraphAsset '{searchName}' not found");
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(path);
        }

        [Test]
        public void Convert_SU_UpdateScore_GraphToCSharp()
        {
            var asset = LoadGraph("SU_UpdateScore");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);

            Assert.IsNotNull(ir);
            Assert.AreEqual("SU_UpdateScore", ir.ClassName);

            var code = CSharpEmitter.Emit(ir);
            Assert.IsNotEmpty(code);

            Debug.Log($"=== SU_UpdateScore ===\n{code}");

            StringAssert.Contains("class SU_UpdateScore", code);
            StringAssert.Contains("MonoBehaviour", code);
        }

        [Test]
        public void Convert_Obstacle_Behavior_ER_GraphToCSharp()
        {
            var asset = LoadGraph("Obstacle_Behavior_ER");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir);

            Assert.IsNotEmpty(code);
            Debug.Log($"=== Obstacle_Behavior_ER ===\n{code}");

            StringAssert.Contains("class Obstacle_Behavior_ER", code);
        }

        [Test]
        public void Convert_PlayerInput_GraphToCSharp()
        {
            var asset = LoadGraph("PlayerInput");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir);

            Assert.IsNotEmpty(code);
            Debug.Log($"=== PlayerInput ===\n{code}");

            StringAssert.Contains("class PlayerInput", code);
        }

        [Test]
        public void Convert_Weapons_GraphToCSharp()
        {
            var asset = LoadGraph("Weapons");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir);

            Assert.IsNotEmpty(code);
            Debug.Log($"=== Weapons ===\n{code}");

            StringAssert.Contains("class Weapons", code);
        }

        [Test]
        public void Convert_SpeedBoost_TutorialBase_GraphToCSharp()
        {
            var asset = LoadGraph("SpeedBoost_TutorialBase");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir);

            Assert.IsNotEmpty(code);
            Debug.Log($"=== SpeedBoost_TutorialBase ===\n{code}");

            StringAssert.Contains("class SpeedBoost_TutorialBase", code);
        }

        [Test]
        public void Convert_ProjectileBehavior_GraphToCSharp()
        {
            var asset = LoadGraph("ProjectileBehavior");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            var reader = new GraphReader();
            var ir = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir);

            Assert.IsNotEmpty(code);
            Debug.Log($"=== ProjectileBehavior ===\n{code}");

            StringAssert.Contains("class ProjectileBehavior", code);
        }

        // Graph → C# → IR のラウンドトリップ
        [Test]
        public void RoundTrip_SU_UpdateScore_CSharpBackToIR()
        {
            var asset = LoadGraph("SU_UpdateScore");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            // Graph → IR → C#
            var reader = new GraphReader();
            var ir1 = reader.Read(asset);
            var code = CSharpEmitter.Emit(ir1);

            // C# → IR
            var parser = new CSharpParser();
            var ir2 = parser.Parse(code);

            Assert.AreEqual(ir1.ClassName, ir2.ClassName);
            Assert.AreEqual(ir1.Methods.Count, ir2.Methods.Count);

            for (var i = 0; i < ir1.Methods.Count; i++)
                Assert.AreEqual(ir1.Methods[i].Name, ir2.Methods[i].Name);
        }

        // Graph → C# → IR → Graph のフルラウンドトリップ
        [Test]
        public void FullRoundTrip_SU_UpdateScore()
        {
            var asset = LoadGraph("SU_UpdateScore");
            if (asset == null) { Assert.Ignore("Sample graph not found"); return; }

            // Graph → IR
            var reader = new GraphReader();
            var ir1 = reader.Read(asset);

            // IR → C#
            var code = CSharpEmitter.Emit(ir1);
            Debug.Log($"=== FullRoundTrip Step 1: C# ===\n{code}");

            // C# → IR
            var parser = new CSharpParser();
            var ir2 = parser.Parse(code);

            // IR → Graph
            var writer = new GraphWriter();
            var graph2 = writer.Write(ir2);

            // Graph → IR (検証用)
            var ir3 = reader.Read(graph2, ir2.ClassName);

            // IR → C# (最終出力)
            var code2 = CSharpEmitter.Emit(ir3);
            Debug.Log($"=== FullRoundTrip Step 2: C# ===\n{code2}");

            Assert.AreEqual(ir1.ClassName, ir3.ClassName);
            StringAssert.Contains("class " + ir1.ClassName, code2);
        }
    }
}
