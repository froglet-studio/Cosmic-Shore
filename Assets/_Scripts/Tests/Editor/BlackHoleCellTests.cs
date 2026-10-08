#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Black Hole cell (Docs/BLACK_HOLE.md §11): a freestyle world that is open water around
    /// one black hole and nothing else, offered by the Cell Selector. Every way it can break is
    /// silent — a config the selector never lists, an environment that is not the hole, a profile
    /// that grows a forest, a scale model the signature filter thins into a broken shell — so each
    /// is asserted from the assets alone.
    /// </summary>
    public class BlackHoleCellTests
    {
        const string ConfigPath = "Assets/_SO_Assets/Cell Configs/Black Hole Cell/Black Hole Cell Config.asset";
        const string PrefabPath = "Assets/_Prefabs/Spawnables/SpawnableBlackHole.prefab";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";

        static CellConfigDataSO LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} is missing.");
            return config;
        }

        [Test]
        public void Config_ContainsJustTheBlackHole()
        {
            var config = LoadConfig();
            Assert.IsInstanceOf<SpawnableBlackHole>(config.EnvironmentPrefab,
                "the Black Hole cell's EnvironmentPrefab is not a SpawnableBlackHole.");
            Assert.IsNull(config.NucleusPrefab, "the Black Hole cell carries a nucleus — the hole is its centre.");
            Assert.IsFalse(config.BootDefault, "the Black Hole cell must stay opt-in, never the freestyle boot world.");
            Assert.IsNotNull(config.SpawnProfile, "the Black Hole cell has no spawn profile.");
            Assert.IsEmpty(config.SpawnProfile.SupportedFloras, "the Black Hole cell's profile grows flora.");
            Assert.IsEmpty(config.SpawnProfile.SupportedFaunas, "the Black Hole cell's profile spawns fauna.");
        }

        [Test]
        public void CellSelector_ListsTheBlackHoleCell()
        {
            // CellSelectorToy authors no list of its own: it offers Menu_Main's Cell.CellConfigs.
            string guid = AssetDatabase.AssetPathToGUID(ConfigPath);
            Assert.IsFalse(string.IsNullOrEmpty(guid), $"{ConfigPath} has no guid.");
            string scene = File.ReadAllText(MenuScenePath);
            StringAssert.Contains($"guid: {guid}", scene,
                "Menu_Main's Cell.CellConfigs does not list the Black Hole cell, so the Cell Selector never offers it.");
        }

        [Test]
        public void ScaleModel_IsEveryHolesBallAndUnderTheSignatureFilter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} is missing.");
            Assert.IsTrue(prefab.TryGetComponent<SpawnableBlackHole>(out var spawnable), "the prefab carries no SpawnableBlackHole.");

            spawnable.InvalidateCache();
            var trails = spawnable.GetTrailData();
            spawnable.InvalidateCache();

            // One ball per hole: the sink at the origin, a dipole's source at its offset.
            var centres = spawnable.IsDipole
                ? new[] { Vector3.zero, spawnable.SourceOffset }
                : new[] { Vector3.zero };
            Assert.AreEqual(centres.Length, trails.Length, "the scale model does not draw one ball per hole.");

            // CellMiniatureBuilder keeps every sample below 64; at 64+ its densest-voxel filter
            // would keep only part of an evenly spread ball.
            int total = 0;
            foreach (var trail in trails) total += trail.Points.Length;
            Assert.That(total, Is.InRange(16, 63), "the scale model's plate count left the band the signature filter keeps whole.");

            for (int t = 0; t < trails.Length; t++)
            {
                var points = trails[t].Points;
                float radius = (points[0].Position - centres[t]).magnitude;
                // Never smaller than a shadow (3√3/2 r_s), and the horizon never shrinks below the floor.
                Assert.Greater(radius, BlackHoleRegistry.Config.MinHorizonRadius,
                    $"ball {t} sits inside the smallest horizon — it is meant to be the shadow or larger.");
                foreach (var point in points)
                    Assert.AreEqual(radius, (point.Position - centres[t]).magnitude, radius * 1e-3f,
                        $"a plate of ball {t} is off its sphere.");
            }
        }
    }
}
#endif
