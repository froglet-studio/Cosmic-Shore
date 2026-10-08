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
        public void Tool_SpawnRowsAreWhereVelocityAndSpin()
        {
            // The spawn rows are strength, size, WHERE (ahead of the camera or a world position),
            // velocity and spin, and the vectors are rows the tool writes straight through to the asset.
            CollectionAssert.AreEqual(
                new[] { "spawnStrength", "spawnHorizonRadius", "spawnAheadOfCamera", "spawnDistanceHorizons",
                        "spawnPosition", "spawnVelocity", "spawnSpinAxis",
                        "pairAheadHorizons", "pairHalfGapHorizons", "pairDriftSpeed", "pairLifetime" },
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
        public void Spawn_LandsAheadOfTheCameraInHorizonRadii_OrAtThePosition()
        {
            var config = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            var cam = new GameObject("spawn camera").transform;
            try
            {
                var fields = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO));
                fields.First(f => f.Name == "spawnHorizonRadius").SetNumber(config, 20f);
                fields.First(f => f.Name == "spawnDistanceHorizons").SetNumber(config, 6f);
                fields.First(f => f.Name == "spawnPosition").SetVector(config, new Vector3(7f, 8f, 9f));
                // A camera at (1,2,3) looking along +x (a vessel's camera mid-flight).
                cam.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));

                fields.First(f => f.Name == "spawnAheadOfCamera").SetBool(config, true);
                var ahead = BlackHoleRegistry.SpawnPoint(config, cam);
                Assert.That(Vector3.Distance(ahead, new Vector3(121f, 2f, 3f)), Is.LessThan(1e-3f),
                    $"6 horizon radii (120 u) ahead of the camera, got {ahead}");
                Assert.AreEqual(new Vector3(7f, 8f, 9f), BlackHoleRegistry.SpawnPoint(config, null),
                    "with no camera the spawn falls back to the spawn position.");

                fields.First(f => f.Name == "spawnAheadOfCamera").SetBool(config, false);
                Assert.AreEqual(new Vector3(7f, 8f, 9f), BlackHoleRegistry.SpawnPoint(config, cam),
                    "with ahead-of-camera off the spawn goes to the spawn position, wherever the camera is.");
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
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

        // ------------------------------------------------------------------ the pair style (§13)

        [Test]
        public void PairStyle_ShipsAsTheDriftPairWithTheCrystalMouthWired()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(ConfigAssetPath);
            Assert.IsNotNull(asset, ConfigAssetPath + " is missing.");
            Assert.IsFalse(asset.CrystalPairs, "the shipped pair style must stay the drift pair until a crystal sling is playtested.");
            Assert.IsNotNull(asset.CrystalMouthMaterial, "crystalMouthMaterial is unwired: a crystal pair would warp space but carry no one.");
            Assert.AreEqual("Assets/_Graphics/Materials/WormholeSeamless.mat", AssetDatabase.GetAssetPath(asset.CrystalMouthMaterial),
                "the crystal pair's mouths must be the seamless ones (Docs/CRYSTAL_WORMHOLE.md §2.2).");
        }

        [Test]
        public void PairStyle_ACrystalSlingLivesAsLongAsADriftSling()
        {
            // The two styles are compared in play (and in the Stoat Flight Studio) on the same clock:
            // form + stand + annihilate = the drift pair's sling life.
            var config = AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(ConfigAssetPath);
            var sling = AssetDatabase.LoadAssetAtPath<StoatSlingConfigSO>("Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset");
            Assert.IsNotNull(sling, "StoatSlingConfig.asset is missing.");
            float crystal = config.CrystalFormSeconds + config.CrystalStandSeconds + config.CrystalAnnihilateSeconds;
            Assert.AreEqual(sling.Lifetime, crystal, 0.01f, "a crystal sling and a drift sling no longer live equally long.");
        }

        [Test]
        public void PairStyle_TheThroatIsTheSizeDialAHorizonIs()
        {
            var config = BlackHoleRegistry.Config;
            var settings = BlackHoleRegistry.CrystalSettings(12f, 0f, Vector3.up, null);
            Assert.AreEqual(config.HorizonRadius(12f, 0f), settings.ThroatRadius, 1e-5f,
                "a crystal pair of strength S must open a throat the size of the horizon a drift pair of S has.");
            Assert.AreEqual(config.CrystalStandSeconds, settings.LifetimeSeconds, 1e-6f,
                "the crystal pair must annihilate on its own (LifetimeSeconds 0 would stand it forever).");
            Assert.Greater(settings.LifetimeSeconds, 0f);
        }

        [Test]
        public void ToolModel_AnAssetReferenceIsShownNotUnsupported()
        {
            var field = BlackHoleToolModel.EditableFields(typeof(BlackHoleConfigSO)).FirstOrDefault(f => f.Name == "crystalMouthMaterial");
            Assert.IsNotNull(field, "crystalMouthMaterial is not a serialized config field.");
            Assert.AreEqual(BlackHoleToolFieldKind.Reference, field.Kind);
        }
    }
}
