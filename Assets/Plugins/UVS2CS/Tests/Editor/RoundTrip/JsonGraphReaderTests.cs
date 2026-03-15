using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UVS2CS.GraphToIR.Serialized;
using UVS2CS.IRToCSharp;

namespace UVS2CS.Tests.RoundTrip
{
    /// <summary>
    /// SerializedGraphParser.Parse → JsonGraphReader.Read → CSharpEmitter.Emit
    /// パイプラインの統合テスト。Roslyn 非依存。
    /// </summary>
    public class JsonGraphReaderTests
    {
        static string ConvertGraph(string searchName)
        {
            var guids = AssetDatabase.FindAssets($"t:ScriptGraphAsset {searchName}");
            if (guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(path);
            if (asset == null) return null;

            var snapshot = SerializedGraphParser.Parse(asset);
            if (snapshot.Units.Count == 0) return null;

            var reader = new JsonGraphReader(snapshot);
            var ir = reader.Read(searchName);
            return CSharpEmitter.Emit(ir);
        }

        [Test]
        public void SpeedBoost_HasClassAndFields()
        {
            var code = ConvertGraph("SpeedBoost");
            if (code == null) { Assert.Ignore("SpeedBoost graph not found"); return; }

            StringAssert.Contains("class SpeedBoost", code);
            StringAssert.Contains("MonoBehaviour", code);
            StringAssert.Contains("bool boostActive", code);
            StringAssert.Contains("float timer", code);
        }

        [Test]
        public void SpeedBoost_OnTriggerEnter_SetsBoostActiveAndTimer()
        {
            var code = ConvertGraph("SpeedBoost");
            if (code == null) { Assert.Ignore("SpeedBoost graph not found"); return; }

            StringAssert.Contains("boostActive = true", code);
            StringAssert.Contains("timer = 1f", code);
        }

        [Test]
        public void SpeedBoost_FixedUpdate_HasBranchOnBoostActive()
        {
            var code = ConvertGraph("SpeedBoost");
            if (code == null) { Assert.Ignore("SpeedBoost graph not found"); return; }

            StringAssert.Contains("if (boostActive)", code);
            StringAssert.Contains("if (timer > 0f)", code);
        }

        [Test]
        public void SpeedBoost_HasOnTriggerEnterAndFixedUpdate()
        {
            var code = ConvertGraph("SpeedBoost");
            if (code == null) { Assert.Ignore("SpeedBoost graph not found"); return; }

            StringAssert.Contains("void OnTriggerEnter()", code);
            StringAssert.Contains("void FixedUpdate()", code);
        }

        [Test]
        public void SU_UpdateScore_HasClass()
        {
            var code = ConvertGraph("SU_UpdateScore");
            if (code == null) { Assert.Ignore("SU_UpdateScore graph not found"); return; }

            StringAssert.Contains("class SU_UpdateScore", code);
            StringAssert.Contains("MonoBehaviour", code);
        }

        [Test]
        public void PlayerInput_HasCustomEventTriggers()
        {
            var code = ConvertGraph("PlayerInput");
            if (code == null) { Assert.Ignore("PlayerInput graph not found"); return; }

            StringAssert.Contains("class PlayerInput", code);
            StringAssert.Contains("CustomEvent.Trigger", code);
        }

        [Test]
        public void PlayerInput_HasMultipleInputHandlers()
        {
            var code = ConvertGraph("PlayerInput");
            if (code == null) { Assert.Ignore("PlayerInput graph not found"); return; }

            StringAssert.Contains("OnInputSystemEventButton", code);
        }

        [Test]
        public void Weapons_HasStartMethod()
        {
            var code = ConvertGraph("Weapons");
            if (code == null) { Assert.Ignore("Weapons graph not found"); return; }

            StringAssert.Contains("class Weapons", code);
            StringAssert.Contains("void Start()", code);
        }

        [Test]
        public void Weapons_HasOnFireCustomEvent()
        {
            var code = ConvertGraph("Weapons");
            if (code == null) { Assert.Ignore("Weapons graph not found"); return; }

            StringAssert.Contains("void OnFire()", code);
        }

        [Test]
        public void Obstacle_Behavior_ER_HasLifecycleMethods()
        {
            var code = ConvertGraph("Obstacle_Behavior_ER");
            if (code == null) { Assert.Ignore("Obstacle_Behavior_ER graph not found"); return; }

            StringAssert.Contains("class Obstacle_Behavior_ER", code);
            StringAssert.Contains("void FixedUpdate()", code);
            StringAssert.Contains("void OnTriggerEnter2D()", code);
        }

        [Test]
        public void ProjectileBehavior_HasTimerAndCustomEvents()
        {
            var code = ConvertGraph("ProjectileBehavior");
            if (code == null) { Assert.Ignore("ProjectileBehavior graph not found"); return; }

            StringAssert.Contains("class ProjectileBehavior", code);
            StringAssert.Contains("CustomEvent.Trigger", code);
        }

        [Test]
        public void ProjectileBehavior_HasMultipleMethods()
        {
            var code = ConvertGraph("ProjectileBehavior");
            if (code == null) { Assert.Ignore("ProjectileBehavior graph not found"); return; }

            StringAssert.Contains("void OnEnable()", code);
            StringAssert.Contains("void FixedUpdate()", code);
        }

        [Test]
        public void SpaceshipBehaviors_HasFieldsAndMethods()
        {
            var code = ConvertGraph("SpaceshipBehaviors");
            if (code == null) { Assert.Ignore("SpaceshipBehaviors graph not found"); return; }

            StringAssert.Contains("class SpaceshipBehaviors", code);
            StringAssert.Contains("float rateOfFire", code);
            StringAssert.Contains("void FixedUpdate()", code);
        }

        [Test]
        public void SpeedBoost_TutorialBase_HasClass()
        {
            var code = ConvertGraph("SpeedBoost_TutorialBase");
            if (code == null) { Assert.Ignore("SpeedBoost_TutorialBase graph not found"); return; }

            StringAssert.Contains("class SpeedBoost_TutorialBase", code);
            StringAssert.Contains("MonoBehaviour", code);
        }

        [Test]
        public void AllGraphs_ProduceNonEmptyOutput()
        {
            var graphNames = new[]
            {
                "SU_UpdateScore", "SpeedBoost", "PlayerInput", "Weapons",
                "Obstacle_Behavior_ER", "ProjectileBehavior", "SpaceshipBehaviors",
                "SpeedBoost_TutorialBase", "SpaceshipBehaviors_TutorialBase",
                "Player_Behavior_ER", "SU_CollisionCheck",
                "SuperUnit_ShootingInput", "Macro_ProjectileBehaviour",
            };

            var convertedCount = 0;
            foreach (var name in graphNames)
            {
                var code = ConvertGraph(name);
                if (code == null)
                {
                    Debug.LogWarning($"[JsonGraphReaderTests] Graph '{name}' not found, skipping");
                    continue;
                }

                Assert.IsNotEmpty(code, $"{name} should produce non-empty output");
                StringAssert.Contains($"class {name}", code, $"{name} should contain its class declaration");
                StringAssert.Contains("MonoBehaviour", code, $"{name} should extend MonoBehaviour");
                convertedCount++;
            }

            Assert.Greater(convertedCount, 0, "At least one sample graph should be convertible");
            Debug.Log($"[JsonGraphReaderTests] AllGraphs: {convertedCount}/{graphNames.Length} graphs converted");
        }
    }
}
