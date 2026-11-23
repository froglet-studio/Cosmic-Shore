using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Flatline (GameModes.Flatline = 84), the Brittle Star's charge game: the open field each shipped prefab lays
    /// (<see cref="FlatlinePanes"/> read with the prefab's own fields) - loose panes, tunnels and cages scattered with a
    /// gap round each - the crystal sites inside the rooms, the scoring (only rounds and cords pay; the ring blast pays
    /// nothing) and the 90-point target. The census rows are the numbers <c>Tools/Build/flatline_budget.py</c> prints;
    /// the generator fails if they and these drift apart.
    /// </summary>
    public class FlatlineTests
    {
        const string PrefabDir = "Assets/_Prefabs/Spawnables";

        /// <summary>The fields of the SHIPPED prefab for an intensity, read from its YAML.</summary>
        static FlatlinePanes.Settings ReadRung(int intensity, out int crystalCount)
        {
            string path = Path.Combine(PrefabDir, $"SpawnableFlatline{intensity}.prefab");
            Assert.IsTrue(File.Exists(path), $"{path} is missing - run Tools/Build/author_flatline_assets.py");
            string yaml = File.ReadAllText(path);
            float F(string key)
            {
                var m = Regex.Match(yaml, $@"^  {key}: ([-0-9.eE]+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{path} has no {key}");
                return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            crystalCount = (int)F("crystalCount");
            return new FlatlinePanes.Settings
            {
                Seed = (int)F("seed"),
                InnerRadius = F("innerRadius"),
                OuterRadius = F("outerRadius"),
                PaneCount = (int)F("paneCount"),
                PaneHalfMin = (int)F("paneHalfMin"),
                PaneHalfMax = Mathf.Max((int)F("paneHalfMin"), (int)F("paneHalfMax")),
                TunnelCount = (int)F("tunnelCount"),
                CageCount = (int)F("cageCount"),
                RoomHalf = (int)F("roomHalf"),
                RibEvery = (int)F("ribEvery"),
                DangerPaneEvery = (int)F("dangerPaneEvery"),
                DangerRoomEvery = (int)F("dangerRoomEvery"),
                HoleShare = F("holeShare"),
                HoleCellTiles = (int)F("holeCellTiles"),
            };
        }

        static List<FlatlinePanes.TileLay> Lay(FlatlinePanes.Settings s, out List<FlatlinePanes.Structure> structures)
        {
            structures = new List<FlatlinePanes.Structure>();
            FlatlinePanes.Layout(s, structures);
            var tiles = new List<FlatlinePanes.TileLay>();
            FlatlinePanes.Tiles(structures, s, tiles);
            return tiles;
        }

        // ── The arena ──

        [TestCase(1, 1560, 0, 360)]
        [TestCase(2, 2280, 0, 560)]
        [TestCase(3, 2914, 168, 646)]
        [TestCase(4, 3194, 640, 670)]
        public void Census_MatchesTheBudgetMirror(int intensity, int total, int danger, int super)
        {
            var tiles = Lay(ReadRung(intensity, out _), out _);
            int d = 0, s = 0;
            foreach (var t in tiles)
            {
                if (t.Kind == PrismKind.Danger) d++;
                else if (t.Kind == PrismKind.SuperShielded) s++;
            }
            Assert.AreEqual(total, tiles.Count, "tile count drifted from flatline_budget.py");
            Assert.AreEqual(danger, d, "danger count drifted from flatline_budget.py");
            Assert.AreEqual(super, s, "rib count drifted from flatline_budget.py");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Field_IsOpen_EveryStructurePlacedWithAGapRoundIt(int intensity)
        {
            var s = ReadRung(intensity, out _);
            var structures = new List<FlatlinePanes.Structure>();
            int placed = FlatlinePanes.Layout(s, structures);
            Assert.AreEqual(s.PaneCount + s.TunnelCount + s.CageCount, placed, "a structure found no room in the field");
            for (int i = 0; i < structures.Count; i++)
            for (int j = i + 1; j < structures.Count; j++)
            {
                var a = structures[i];
                var b = structures[j];
                float clear = Vector3.Distance(a.Centre, b.Centre) - a.Radius - b.Radius;
                Assert.GreaterOrEqual(clear, FlatlinePanes.Gap - 1e-2f,
                    $"I{intensity}: structures {a.Index} and {b.Index} stand {clear:F1} u apart, under the gap");
            }
        }

        [Test]
        public void Ladder_AddsRoomsYouCanFlyInto()
        {
            var i1 = ReadRung(1, out _);
            Assert.AreEqual(0, i1.TunnelCount + i1.CageCount, "I1 is loose panes only");
            Assert.Greater(ReadRung(2, out _).TunnelCount, 0, "I2 adds the tunnels");
            Assert.Greater(ReadRung(3, out _).CageCount, 0, "I3 adds the cages");
            // A cage's walls keep a window at their middle; count the gap on one wall of a lone cage.
            var cage = new FlatlinePanes.Settings { Seed = 1, InnerRadius = 0f, OuterRadius = 1f, CageCount = 1, RoomHalf = 4 };
            var tiles = Lay(cage, out var st);
            int wallHalf = cage.RoomHalf - 1, side = 2 * wallHalf + 1, window = 2 * FlatlinePanes.WindowHalf + 1;
            Assert.AreEqual(6 * (side * side - window * window), tiles.Count, "a cage is six walls, each with a window");
            Assert.AreEqual(1, st.Count);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CrystalSites_SitInsideTheRoomsFirst(int intensity)
        {
            var s = ReadRung(intensity, out int count);
            Lay(s, out var structures);
            var sites = new List<Vector3>();
            FlatlinePanes.CrystalSites(structures, count, sites);
            Assert.AreEqual(count, sites.Count);
            int rooms = 0;
            foreach (var st in structures)
                if (st.Kind != FlatlinePanes.KindPane) rooms++;
            for (int k = 0; k < sites.Count; k++)
            {
                Assert.Less(sites[k].magnitude, 580f, "a crystal sits out by the spawn ring");
                if (k < rooms)
                {
                    bool inRoom = false;
                    foreach (var st in structures)
                        if (st.Kind != FlatlinePanes.KindPane && (sites[k] - st.Centre).sqrMagnitude < 1e-4f) inRoom = true;
                    Assert.IsTrue(inRoom, $"I{intensity}: site {k} is not at a room's centre");
                }
            }
        }

        // ── The scoring ──

        [Test]
        public void Rule_PaysRoundsAndCords_NeverTheRing()
        {
            var rule = ScriptableObject.CreateInstance<FlatlineScoringRuleSO>();
            try
            {
                Assert.AreEqual(1, rule.PointsForCombatHit(CombatHitClass.Bullet), "a round is the fleet's 1");
                Assert.AreEqual(8, rule.PointsForCombatHit(CombatHitClass.Strike), "a cord or saber Strike is the fleet's 8");
                Assert.AreEqual(0, rule.PointsForCombatHit(CombatHitClass.MissileBlast), "the ring blast pays nothing");
                Assert.AreEqual(0, rule.PointsForCombatHit(CombatHitClass.MissileShockwave));
                Assert.AreEqual(0, rule.PointsForCombatHit(CombatHitClass.MissileDirect));
                Assert.AreEqual(0, rule.PointsForCombatHit(CombatHitClass.Debuff));
                Assert.AreEqual(CombatHitDrain.PointsFor(CombatHitClass.Bullet), rule.RoundPoints);
                Assert.AreEqual(CombatHitDrain.PointsFor(CombatHitClass.Strike), rule.StrikePoints);
            }
            finally { Object.DestroyImmediate(rule); }
        }

        [Test]
        public void Target_Is90_AndFallsBackToTheScalar()
        {
            var eco = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                for (int i = 1; i <= 4; i++) Assert.AreEqual(90, eco.GetFlatlinePointTarget(i), "90 to win at every rung");
                eco.flatlinePointTargetByIntensity = new List<int> { 0, 0, 0, 0 };
                eco.flatlinePointTarget = 0;
                Assert.AreEqual(EndConditionOverridesSO.DefaultFlatlinePointTarget, eco.GetFlatlinePointTarget(3),
                    "a rung with nothing to say falls back to the scalar, never to 0");
                Assert.IsTrue(eco.TryGetAuthoredTurnTarget(GameModes.Flatline, out int t) && t > 0);
            }
            finally { Object.DestroyImmediate(eco); }
        }
    }
}
