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
    /// Flatline (GameModes.Flatline = 85), the Brittle Star's charge game: the arena each shipped prefab lays
    /// (<see cref="FlatlinePanes"/> read with the prefab's own fields), the flush rule where panes cross, the crystal
    /// sites on the seams, the scoring (only rounds and cords pay; the ring blast pays nothing) and the per-intensity
    /// target. The census rows are the numbers <c>Tools/Build/flatline_budget.py</c> prints; the generator fails if
    /// they and these drift apart.
    /// </summary>
    public class FlatlineTests
    {
        const string PrefabDir = "Assets/_Prefabs/Spawnables";

        struct Rung
        {
            public int Arrangement, RadiusTiles, RibEvery, DangerRimTiles, HoleCellTiles, CrystalCount;
            public float Offset, HoleShare, CrystalSpacing;
        }

        /// <summary>The fields of the SHIPPED prefab for an intensity, read from its YAML.</summary>
        static Rung ReadRung(int intensity)
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
            return new Rung
            {
                Arrangement = (int)F("arrangement"),
                RadiusTiles = (int)F("paneRadiusTiles"),
                Offset = F("paneOffset"),
                RibEvery = (int)F("ribEvery"),
                DangerRimTiles = (int)F("dangerRimTiles"),
                HoleShare = F("holeShare"),
                HoleCellTiles = (int)F("holeCellTiles"),
                CrystalCount = (int)F("crystalCount"),
                CrystalSpacing = F("crystalSpacing"),
            };
        }

        static List<FlatlinePanes.TileLay> Lay(Rung r, out List<FlatlinePanes.Pane> panes)
        {
            panes = new List<FlatlinePanes.Pane>();
            FlatlinePanes.Panes(r.Arrangement, r.RadiusTiles, r.Offset, panes);
            var tiles = new List<FlatlinePanes.TileLay>();
            FlatlinePanes.Tiles(panes, r.DangerRimTiles, r.RibEvery, r.HoleShare, r.HoleCellTiles, tiles);
            return tiles;
        }

        // ── The arena ──

        [TestCase(1, 4819, 0, 1363)]
        [TestCase(2, 8393, 0, 2393)]
        [TestCase(3, 7217, 1372, 1813)]
        [TestCase(4, 5440, 1568, 1710)]
        public void Census_MatchesTheBudgetMirror(int intensity, int total, int danger, int super)
        {
            var tiles = Lay(ReadRung(intensity), out _);
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
        public void CrossingPanes_NeverInterpenetrate(int intensity)
        {
            // Every pair of tiles from DIFFERENT panes whose centres are close enough to touch must be separated along
            // one of the two panes' normals by more than a tile's reach plus the other's half-thickness (the seam's
            // guarantee; Docs/ECOSYSTEM.md §62). Bucketed so the check is not quadratic.
            var tiles = Lay(ReadRung(intensity), out var panes);
            float reach = Mathf.Sqrt(25f + 25f + 1f) + FlatlinePanes.Thickness * 0.5f;
            const float cellSize = 16f;
            var buckets = new Dictionary<Vector3Int, List<int>>();
            Vector3Int Key(Vector3 p) => new(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize),
                                             Mathf.FloorToInt(p.z / cellSize));
            for (int i = 0; i < tiles.Count; i++)
            {
                var k = Key(tiles[i].Position);
                if (!buckets.TryGetValue(k, out var list)) buckets[k] = list = new List<int>();
                list.Add(i);
            }
            int checkedPairs = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                var a = tiles[i];
                var k = Key(a.Position);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!buckets.TryGetValue(k + new Vector3Int(dx, dy, dz), out var list)) continue;
                    foreach (int j in list)
                    {
                        if (j <= i || tiles[j].Pane == a.Pane) continue;
                        var b = tiles[j];
                        Vector3 delta = b.Position - a.Position;
                        if (delta.magnitude > 2f * reach) continue;
                        checkedPairs++;
                        float alongA = Mathf.Abs(Vector3.Dot(delta, panes[a.Pane].Normal));
                        float alongB = Mathf.Abs(Vector3.Dot(delta, panes[b.Pane].Normal));
                        Assert.IsTrue(alongA > reach - 1e-3f || alongB > reach - 1e-3f,
                            $"I{intensity}: tiles {i} (pane {a.Pane}) and {j} (pane {b.Pane}) may interpenetrate " +
                            $"({alongA:F2} / {alongB:F2} u apart along the normals, {reach:F2} needed)");
                    }
                }
            }
            if (intensity > 1) Assert.Greater(checkedPairs, 0, "no crossing tiles came near each other - the check saw nothing");
        }

        [Test]
        public void Seams_CarveTheLaterPanes()
        {
            // Negative control for the seam rule: three full discs of radius 23 hold 3 x 1,661 tiles; the laid I1 must be
            // fewer, or the later panes were not carved where they cross the first.
            int disc = 0;
            for (int a = -23; a <= 23; a++)
            for (int b = -23; b <= 23; b++)
                if (a * a + b * b <= 23 * 23) disc++;
            var tiles = Lay(ReadRung(1), out _);
            Assert.Less(tiles.Count, 3 * disc);
            Assert.Greater(tiles.Count, 3 * disc - 400, "the seams carved far more than two slots per crossing");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CrystalSites_SitOnTheSeams(int intensity)
        {
            var r = ReadRung(intensity);
            Lay(r, out var panes);
            var sites = new List<Vector3>();
            FlatlinePanes.CrystalSites(panes, r.CrystalCount, r.CrystalSpacing, sites);
            Assert.AreEqual(r.CrystalCount, sites.Count);
            foreach (var s in sites)
            {
                Assert.Less(s.magnitude, 320f, "a crystal sits out by the spawn ring");
                int onPlanes = 0;
                foreach (var p in panes)
                    if (Mathf.Abs(Vector3.Dot(s - p.Centre, p.Normal)) < 0.05f) onPlanes++;
                Assert.GreaterOrEqual(onPlanes, 2, $"site {s} is not on a seam (on {onPlanes} pane planes)");
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
        public void Target_IsPerIntensity_AndFallsBackToTheScalar()
        {
            var eco = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
            try
            {
                Assert.AreEqual(180, eco.GetFlatlinePointTarget(1));
                Assert.AreEqual(200, eco.GetFlatlinePointTarget(2));
                Assert.AreEqual(220, eco.GetFlatlinePointTarget(3));
                Assert.AreEqual(240, eco.GetFlatlinePointTarget(4));
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
