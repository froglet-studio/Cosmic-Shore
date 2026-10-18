#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Rampage's intensity ladder, pinned at both ends: the two pure formulas, and the
    /// AUTHORED DATA that feeds them.
    ///
    /// <para>Intensity 4 is the shipped, play-tested arena; intensity 1 is the same arena
    /// made BIGGER, DENSER AND EASIER TO HIT (Tools/Build/rampage_intensity.py, rev 2026-09;
    /// Docs/ECOSYSTEM.md §43, which supersedes the flat forest of §29). The ladder moves:</para>
    /// <list type="bullet">
    ///   <item><b>Flora goes DOWN</b> - 5.00x, 3.67x, 2.33x, 1.00x the authored plant count,
    ///   at full per-plant budget, with a 1.60x / 1.40x / 1.20x / 1.00x leaf.</item>
    ///   <item><b>Crystals go DOWN</b> - 2x players, 1x players, players-1, then exactly 1.
    ///   The crystal is the Dolphin's only blast trigger, so this is how contested the
    ///   discharge is.</item>
    ///   <item><b>Wildlife goes UP</b> - 1x, 2x, 3x, 4x the authored population.</item>
    /// </list>
    ///
    /// <para><b>Why these read the asset text.</b> The crystal ladder lives in the SCENE and
    /// the fauna ladder in four SO assets, and neither is reachable from the model script's
    /// <c>--check</c> (it only regenerates the SOs, and cannot see the scene at all). An
    /// inverted sign or a dropped entry in either place is silent: the mode still runs, it
    /// just stops meaning what it says. A text scan is the only gate that covers the scene,
    /// and it costs one file read.</para>
    /// </summary>
    public class RampageIntensityLadderTests
    {
        const string ScenePath = "_Scenes/Multiplayer Scenes/MinigameRampage.unity";
        const string ProfileDir = "_SO_Assets/Cell Configs/Rampage Cell";

        static string Asset(string relative) => Path.Combine(Application.dataPath, relative);

        // ── The formulas ────────────────────────────────────────────────────

        static readonly List<IntensityCrystalCount> RampageLadder = new()
        {
            new IntensityCrystalCount { CrystalsPerPlayer = 2f, ExtraCrystals = 0 },
            new IntensityCrystalCount { CrystalsPerPlayer = 1f, ExtraCrystals = 0 },
            new IntensityCrystalCount { CrystalsPerPlayer = 1f, ExtraCrystals = -1 },
            new IntensityCrystalCount { CrystalsPerPlayer = 0f, ExtraCrystals = 1 },
        };

        [Test]
        // intensity, players, expected crystals
        [TestCase(1, 1, 2)] [TestCase(1, 2, 4)] [TestCase(1, 4, 8)]
        [TestCase(2, 1, 1)] [TestCase(2, 2, 2)] [TestCase(2, 4, 4)]
        [TestCase(3, 1, 1)] [TestCase(3, 2, 1)] [TestCase(3, 4, 3)]   // players-1, floored at 1
        [TestCase(4, 1, 1)] [TestCase(4, 2, 1)] [TestCase(4, 4, 1)]   // one, whatever the roster
        public void CrystalLadder_MatchesTheSpec(int intensity, int players, int expected)
        {
            Assert.AreEqual(expected,
                CrystalManager.ResolveIntensityCrystalCount(RampageLadder, intensity, players));
        }

        [Test]
        public void CrystalCount_NeverDropsBelowOne_AndClampsIntensityToTheTable()
        {
            // A solo player at intensity 3 would be 1-1 = 0 without the floor: no crystal at
            // all, and the Dolphin could never discharge a blast.
            Assert.AreEqual(1, CrystalManager.ResolveIntensityCrystalCount(RampageLadder, 3, 1));

            // Out-of-range intensity reuses the nearest end rather than throwing.
            Assert.AreEqual(CrystalManager.ResolveIntensityCrystalCount(RampageLadder, 1, 3),
                            CrystalManager.ResolveIntensityCrystalCount(RampageLadder, 0, 3));
            Assert.AreEqual(CrystalManager.ResolveIntensityCrystalCount(RampageLadder, 4, 3),
                            CrystalManager.ResolveIntensityCrystalCount(RampageLadder, 9, 3));

            // An unauthored table is a wiring mistake, not a crash: fall back to one each.
            Assert.AreEqual(3, CrystalManager.ResolveIntensityCrystalCount(null, 2, 3));
        }

        [Test]
        public void FaunaPopulationScale_ScalesSeedCounts_AndLeavesUncappedUncapped()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfileSO>();
            try
            {
                profile.FaunaPopulationScale = 3f;
                Assert.AreEqual(12, profile.ScaleFaunaPopulation(4), "seed floor should scale");
                Assert.AreEqual(18, profile.ScaleFaunaPopulation(6), "live cap should scale");

                // 0 means "uncapped" on MaxLivePopulation - scaling must not invent a cap.
                Assert.AreEqual(0, profile.ScaleFaunaPopulation(0));

                // Round half UP, not banker's: 1 x 1.5 must not fall to 2's neighbour rule.
                profile.FaunaPopulationScale = 1.5f;
                Assert.AreEqual(2, profile.ScaleFaunaPopulation(1));
                Assert.AreEqual(5, profile.ScaleFaunaPopulation(3));

                // A scale of 1 - and an unauthored 0 - leave every biome exactly as authored.
                profile.FaunaPopulationScale = 1f;
                Assert.AreEqual(7, profile.ScaleFaunaPopulation(7));
                profile.FaunaPopulationScale = 0f;
                Assert.AreEqual(7, profile.ScaleFaunaPopulation(7));

                // Never scales a live species out of existence.
                profile.FaunaPopulationScale = 0.1f;
                Assert.AreEqual(1, profile.ScaleFaunaPopulation(2));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        // ── The authored data ───────────────────────────────────────────────

        [Test]
        public void RampageScene_AuthorsTheCrystalLadder()
        {
            string path = Asset(ScenePath);
            Assert.IsTrue(File.Exists(path), $"Rampage scene missing at {path}");
            string yaml = File.ReadAllText(path);

            StringAssert.Contains("crystalCountMode: 2", yaml,
                "Rampage's CrystalManager must be on CrystalCountMode.IntensityScaled (2); " +
                "FixedCount would pin every intensity to one crystal again.");

            var entries = Regex.Matches(yaml,
                @"-\s+CrystalsPerPlayer:\s*(-?[\d.]+)\s*\n\s*ExtraCrystals:\s*(-?\d+)");
            Assert.AreEqual(4, entries.Count,
                "Expected exactly four crystalCountByIntensity entries, one per intensity.");

            var expected = new[] { (2f, 0), (1f, 0), (1f, -1), (0f, 1) };
            for (int i = 0; i < 4; i++)
            {
                float perPlayer = float.Parse(entries[i].Groups[1].Value, CultureInfo.InvariantCulture);
                int extra = int.Parse(entries[i].Groups[2].Value, CultureInfo.InvariantCulture);
                Assert.AreEqual(expected[i].Item1, perPlayer, 1e-4f, $"intensity {i + 1} CrystalsPerPlayer");
                Assert.AreEqual(expected[i].Item2, extra, $"intensity {i + 1} ExtraCrystals");
            }
        }

        [Test]
        public void RampageSpawnProfiles_ClimbTheFaunaLadder_AndThinTheForestTowardTheShippedArena()
        {
            // The model's FAUNA_SCALES, SCALES (flora count, plant budget) and PRISM_SCALES. These
            // profiles are generated by Tools/Build/rampage_intensity.py (--check gates them
            // against the model); this pins the ladder's SHAPE from the asset side.
            var expectedFauna = new[] { 1f, 2f, 3f, 4f };
            var expectedFlora = new[] { 5.00f, 3.67f, 2.33f, 1.00f };
            var expectedPrism = new[] { 1.60f, 1.40f, 1.20f, 1.00f };

            for (int i = 1; i <= 4; i++)
            {
                string path = Asset(Path.Combine(ProfileDir, $"Rampage Spawn Profile {i}.asset"));
                Assert.IsTrue(File.Exists(path), $"Rampage Spawn Profile {i} missing at {path}");
                string yaml = File.ReadAllText(path);

                Assert.AreEqual(expectedFauna[i - 1], ReadFloat(yaml, "FaunaPopulationScale", i), 1e-4f,
                    $"intensity {i} must carry {expectedFauna[i - 1]}x the authored wildlife");

                // The forest THICKENS toward intensity 1 and lands on the shipped arena at 4:
                // more PLANTS (never a bigger per-plant budget, which would compound with the
                // prism scale on the same prisms) and fatter leaves. Intensity 4 is exactly 1.0
                // on both, so the play-tested arena is untouched.
                Assert.AreEqual(expectedFlora[i - 1], ReadFloat(yaml, "FloraPopulationScale", i), 1e-4f,
                    $"intensity {i} must grow {expectedFlora[i - 1]}x the authored plants (FloraPopulationScale)");
                Assert.AreEqual(1f, ReadFloat(yaml, "FloraPlantBudgetScale", i), 1e-4f,
                    $"intensity {i} must grow full-size plants (FloraPlantBudgetScale 1)");
                Assert.AreEqual(expectedPrism[i - 1], ReadFloat(yaml, "FloraPrismScale", i), 1e-4f,
                    $"intensity {i} must lay a {expectedPrism[i - 1]}x leaf (FloraPrismScale)");
            }
        }

        static float ReadFloat(string yaml, string key, int intensity)
        {
            var m = Regex.Match(yaml, $@"^\s*{Regex.Escape(key)}:\s*(-?[\d.eE+]+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"intensity {intensity} spawn profile has no '{key}' field");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
#endif
