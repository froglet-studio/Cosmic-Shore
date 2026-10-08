using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Black Hole tool (Docs/BLACK_HOLE.md §6.1): it reaches every field of the config ASSET as
    /// Unity itself serializes it, spawns at the asset's spawn position from its Spawn section, writes
    /// inside each field's own bounds, and builds and closes its panel. The same model is also run
    /// offline by Tools/Build/black_hole_tool_harness.
    /// </summary>
    public class BlackHoleToolTests
    {
        const string ConfigAssetPath = "Assets/Resources/BlackHoleConfig.asset";

        [Test]
        public void Tool_ReachesEveryFieldUnitySerializesOnTheConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(ConfigAssetPath);
            Assert.IsNotNull(asset, ConfigAssetPath + " is missing.");

            // Unity's own view of the serialized fields, top level.
            var unity = new List<string>();
            var so = new SerializedObject(asset);
            var it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
                if (it.name != "m_Script") unity.Add(it.name);

            var tool = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));
            CollectionAssert.AreEquivalent(unity, tool.Select(f => f.Name).ToList(),
                "the tool's fields are not the fields Unity serializes on BlackHoleConfigSO");
            Assert.IsFalse(tool.Any(f => f.Kind == BlackHoleToolFieldKind.Unsupported),
                "a config field has a type the tool cannot draw: " +
                string.Join(", ", tool.Where(f => f.Kind == BlackHoleToolFieldKind.Unsupported).Select(f => f.Name)));
            foreach (var name in BlackHoleToolModel.SpawnFieldNames)
                Assert.IsTrue(tool.Any(f => f.Name == name), $"the tool's spawn row '{name}' has no config field.");
        }

        [Test]
        public void Tool_SpawnRowsArePositionVelocityAndSpinInWorldSpace()
        {
            // The Spawn button places the hole at the config's spawn POSITION — no camera involved — so
            // the spawn rows are exactly strength, size, position, velocity, spin, and the three vectors
            // are drawn as vector rows the tool writes straight through to the asset.
            CollectionAssert.AreEqual(
                new[] { "spawnStrength", "spawnHorizonRadius", "spawnPosition", "spawnVelocity", "spawnSpinAxis" },
                BlackHoleToolModel.SpawnFieldNames);
            var fields = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));
            var config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            try
            {
                var position = fields.First(f => f.Name == "spawnPosition");
                Assert.AreEqual(BlackHoleToolFieldKind.Vector3, position.Kind);
                position.SetVector(config, new Vector3(120f, -40f, 300f));
                Assert.AreEqual(new Vector3(120f, -40f, 300f), config.SpawnPosition, "the spawn position row does not reach the config.");
                fields.First(f => f.Name == "spawnVelocity").SetVector(config, new Vector3(0f, 0f, -60f));
                Assert.AreEqual(new Vector3(0f, 0f, -60f), config.SpawnVelocity, "spawn velocity is world space, stored as typed.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Config_SizeIsIndependentOfStrengthWhenSet()
        {
            var config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            try
            {
                Assert.AreEqual(config.HorizonRadius(10f), config.HorizonRadius(10f, 0f), 1e-5f, "size 0 must derive r_s from strength.");
                Assert.AreEqual(35f, config.HorizonRadius(10f, 35f), 1e-5f, "a set size must win over the strength.");
                Assert.AreEqual(35f, config.HorizonRadius(80f, 35f), 1e-5f, "a set size must not follow the strength.");
                Assert.GreaterOrEqual(config.InfluenceRadius(10f, 35f), 1.5f * 35f - 1e-3f, "a hole must pull the shell around its own horizon.");
                Assert.AreEqual(35f * (config.WarpReachMultiplier - 1f), config.WarpReachForHorizon(35f), 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Tool_WritesLandInsideEachFieldsOwnBounds()
        {
            var config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            try
            {
                var fields = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));
                fields.First(f => f.Name == "spawnStrength").SetNumber(config, 500f);
                Assert.AreEqual(100f, config.SpawnStrength, "spawnStrength past its [Range] max.");
                fields.First(f => f.Name == "maxBlackHoles").SetNumber(config, 7.6f);
                Assert.AreEqual(4, config.MaxBlackHoles, "maxBlackHoles past its [Range] max.");
                fields.First(f => f.Name == "maxBodies").SetNumber(config, -5f);
                Assert.AreEqual(0, config.MaxBodies, "maxBodies below its [Min].");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Console_ToolSwitchParses()
        {
            Assert.AreEqual(true, BlackHoleToolModel.ParseSwitch("on", out bool a));
            Assert.IsTrue(a);
            Assert.AreEqual(false, BlackHoleToolModel.ParseSwitch("off", out bool b));
            Assert.IsTrue(b);
            Assert.IsNull(BlackHoleToolModel.ParseSwitch(null, out bool c));
            Assert.IsTrue(c, "no word is a toggle");
            BlackHoleToolModel.ParseSwitch("sideways", out bool d);
            Assert.IsFalse(d, "an unknown word must be reported, not read as a toggle");
        }

        /// <summary>The real panel: built, one live control per config field, config view shown, closed.</summary>
        [Test]
        public void Tool_BuildsAControlForEveryConfigFieldAndCloses()
        {
            var systemsBefore = new HashSet<EventSystem>(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                BlackHoleTool.SetOpen(true, showConfig: true);
                Assert.IsTrue(BlackHoleTool.IsOpen, "the tool did not open.");
                Assert.IsTrue(BlackHoleTool.IsConfigShown, "blackhole config must open the config view.");
                Assert.AreEqual(BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO)).Count, BlackHoleTool.BoundFieldCount,
                    "a config field has no live control in the tool.");

                BlackHoleTool.SetOpen(false);
                Assert.IsFalse(BlackHoleTool.IsOpen, "the tool did not close.");
            }
            finally
            {
                var go = GameObject.Find("[BlackHoleTool]");
                if (go != null) Object.DestroyImmediate(go);
                foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!systemsBefore.Contains(es)) Object.DestroyImmediate(es.gameObject);
            }
        }
    }
}
