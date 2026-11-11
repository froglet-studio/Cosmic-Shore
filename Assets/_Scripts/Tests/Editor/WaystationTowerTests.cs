using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Waystation's Drop Towers (<see cref="WaystationTower"/>, Docs/Studios/TIGER_MOTH_TIME.md). The full
    /// verdicts - every hop's window, runway, corporeal clearance, the cell, the long last gap - are
    /// <c>Tools/Build/waystation_tower_course.py --check</c>, which mirrors this class over 200 seeds x 4
    /// intensities. These pin the properties on the shipped C# itself.
    /// </summary>
    [TestFixture]
    public class WaystationTowerTests
    {
        const int Clusters = 5;

        static WaystationCourseSettings Course(int intensity)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.RingTarget = Clusters * s.RingsPerCluster;
            s.InnerRadius = 478f;
            s.OuterRadius = 1080f;
            s.FirstClusterDirection = Vector3.up;
            return s;
        }

        [Test]
        public void GarrettsClustersAreLaidUnchanged()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 20; seed++)
            {
                var course = Course(intensity);
                var laid = WaystationTower.Build(seed, course, intensity, out int used);
                var clusters = WaystationCourse.Generate(used, course);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                int k = 0;
                foreach (var gate in laid)
                {
                    if (WaystationTower.IsTowerRing(gate, t)) continue;
                    Assert.Less(k, clusters.Count, $"i{intensity} seed {seed}: more cluster rings than Waystation lays");
                    Assert.AreEqual(clusters[k].Position, gate.Position, $"i{intensity} seed {seed} ring {k} moved");
                    Assert.AreEqual(clusters[k].Axis, gate.Axis, $"i{intensity} seed {seed} ring {k} turned");
                    k++;
                }
                Assert.AreEqual(clusters.Count, k, $"i{intensity} seed {seed}: a cluster ring went missing");
            }
        }

        [Test]
        public void TheLastGapIsTheLongDropAndHasNoTower()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 20; seed++)
            {
                var course = Course(intensity);
                var laid = WaystationTower.Build(seed, course, intensity, out _);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                int per = course.RingsPerCluster;
                int lastExit = laid.Count - per - 1;   // the last cluster is the last `per` gates
                Assert.IsFalse(WaystationTower.IsTowerRing(laid[lastExit], t),
                    $"i{intensity} seed {seed}: the long final gap carries a tower");
                Assert.GreaterOrEqual(Vector3.Distance(laid[lastExit].Position, laid[lastExit + 1].Position),
                    WaystationTower.LongGapMin, $"i{intensity} seed {seed}: the last gap is too short to leave a wormhole");
            }
        }

        [Test]
        public void TowerRingsAreNeverClusterSized()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                Assert.AreNotEqual(WaystationCourseSettings.ForIntensity(intensity).RingRadius,
                    WaystationTowerSettings.ForIntensity(intensity).HopRingRadius,
                    $"i{intensity}: peers tell tower rings by radius, so it must differ from a cluster ring's");
        }

        [Test]
        public void TheWindowClearsThePlateAndTheRunwayCoversTheRechargeAndTheRoll()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var t = WaystationTowerSettings.ForIntensity(intensity);
                Assert.GreaterOrEqual(t.PlateDepth, WaystationTower.HullRadius + WaystationTower.LandClear,
                    $"i{intensity}: the floor plate touches the cruise line");
                float landing = WaystationTower.Travel(WaystationTower.HoldForTravel(t.WindowLow));
                Assert.AreEqual(t.WindowLow, landing, 0.5f, $"i{intensity}: HoldForTravel does not invert Travel");
                foreach (float roll in new[] { 0f, 90f, 180f })
                {
                    float seconds = WaystationTower.Runway(t, roll) / WaystationTower.Cruise;
                    Assert.GreaterOrEqual(seconds, WaystationTower.RechargeForTravel(t.WindowHigh),
                        $"i{intensity}: the runway is shorter than the far edge's recharge");
                    Assert.GreaterOrEqual(seconds, WaystationTower.RollSeconds(roll),
                        $"i{intensity}: the runway is shorter than a {roll} degree roll");
                }
            }
        }

        [Test]
        public void TheFallMatchesTheDropsOwnNumbers()
        {
            // TigerMothDropMath.HoldSecondsFor is the executor's own inverse for full-speed holds; past the ease the
            // closed form here must agree with it.
            foreach (float travel in new[] { 250f, 400f, 900f })
            {
                float hold = WaystationTower.HoldForTravel(travel);
                float executor = TigerMothDropMath.HoldSecondsFor(travel, WaystationTower.DropSpeed,
                    WaystationTower.DropEaseSeconds, WaystationTower.LandSeconds, WaystationTower.Cruise);
                Assert.AreEqual(executor, hold, 0.002f, $"travel {travel}: the tower and the drop disagree on the hold");
            }
        }

        [Test]
        public void DangerTilesTakeTheirShareTheSameOnEveryPeer()
        {
            foreach (float share in new[] { 0.10f, 0.20f, 0.35f, 0.50f })
            {
                int danger = 0, n = 0;
                for (int gate = 0; gate < 40; gate++)
                for (int tile = 0; tile < 160; tile++, n++)
                {
                    bool a = WaystationTower.IsDangerTile(gate, gate % 2, tile, share);
                    Assert.AreEqual(a, WaystationTower.IsDangerTile(gate, gate % 2, tile, share), "the hash is not stable");
                    if (a) danger++;
                }
                Assert.AreEqual(share, danger / (float)n, 0.03f, $"share {share}");
            }
            Assert.IsFalse(WaystationTower.IsDangerTile(3, 0, 5, 0f), "share 0 must lay no danger");
            Assert.IsTrue(WaystationTower.IsDangerTile(3, 0, 5, 1f), "share 1 must lay only danger");
        }

        // ── Lantern Towers (2026-11-11) ──────────────────────────────────────

        static float Span(IReadOnlyList<RaceGate> laid, int i, in WaystationTowerSettings t) =>
            Vector3.Dot(WaystationTower.PressOf(laid, i + 1, t) - WaystationTower.PressOf(laid, i, t), laid[i].Axis);

        static float Need(float landing, float roll) =>
            Mathf.Max(WaystationTower.RechargeForTravel(landing), WaystationTower.RollSeconds(roll));

        [Test]
        public void TheLaidDepthKeepsTheReactionAndTheShelfPressesExactlyOnTime()
        {
            int hops = 0;
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 40; seed++)
            {
                var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                for (int i = 1; i < laid.Count; i++)
                {
                    if (!WaystationTower.HasShelf(laid, i, t)) continue;
                    hops++;
                    float laidDepth = WaystationTower.LaidDepth(laid, i, t);
                    float shelf = WaystationTower.ShelfDepth(laid, i, t);
                    float far = WaystationTower.FarEdge(laid, i, t);
                    float roll = WaystationTower.RollBefore(laid, i);
                    float span = Span(laid, i, t);
                    string at = $"i{intensity} seed {seed} ring {i}";
                    Assert.LessOrEqual(laidDepth, shelf + 0.1f, $"{at}: the laid depth is deeper than the shelf");
                    Assert.LessOrEqual(shelf, far + 0.1f, $"{at}: the shelf is past the far edge");
                    Assert.GreaterOrEqual((span - laidDepth) / WaystationTower.Cruise,
                        Need(laidDepth, roll) + WaystationTower.ReactSeconds - 0.01f, $"{at}: the laid depth keeps no reaction");
                    Assert.AreEqual(Need(shelf, roll), (span - shelf) / WaystationTower.Cruise, 0.01f,
                        $"{at}: a landing at the shelf is not recharged and rolled exactly at the next press point");
                }
            }
            Assert.Greater(hops, 100, "too few hops to say anything");
        }

        [Test]
        public void ALandingPastTheFarEdgeCannotReachTheNextRing()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 40; seed++)
            {
                var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                for (int i = 1; i < laid.Count; i++)
                {
                    if (!WaystationTower.HasShelf(laid, i, t)) continue;
                    float landing = WaystationTower.FarEdge(laid, i, t) + 5f;
                    float late = (Need(landing, WaystationTower.RollBefore(laid, i))
                                  - (Span(laid, i, t) - landing) / WaystationTower.Cruise) * WaystationTower.Cruise;
                    Assert.Greater(late, laid[i + 1].Radius - WaystationTower.HullRadius,
                        $"i{intensity} seed {seed} ring {i}: a landing past the far edge still threads the next ring");
                }
            }
        }

        [Test]
        public void EveryHopIsLooseAtI1AndTheShelfShareHoldsFromI2()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                var t = WaystationTowerSettings.ForIntensity(intensity);
                int tight = 0;
                for (int gate = 0; gate < 4000; gate++)
                    if (WaystationTower.IsTightHop(gate, t)) tight++;
                Assert.AreEqual(t.ShelfShare, tight / 4000f, 0.03f, $"i{intensity}: the hash does not draw the authored share");
                bool anyTight = false;
                for (int seed = 1; seed <= 20 && !anyTight; seed++)
                {
                    var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                    for (int i = 1; i < laid.Count; i++)
                        if (WaystationTower.HasShelf(laid, i, t)
                            && WaystationTower.LaidDepth(laid, i, t) < t.WindowHigh - 1f)
                            anyTight = true;
                }
                Assert.AreEqual(intensity >= 2, anyTight, $"i{intensity}: tight hops where the ladder says none, or none where it says some");
            }
        }

        [Test]
        public void EveryPeerLaysTheSameLanternsAndNoneStandsInAMouth()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
            int stood = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                var a = new List<WaystationLantern>();
                var b = new List<WaystationLantern>();
                WaystationTower.Lanterns(laid, intensity, a);
                WaystationTower.Lanterns(new List<RaceGate>(laid), intensity, b);
                Assert.AreEqual(a.Count, b.Count);
                const float pad = WaystationTower.LanternSeg * 0.5f + WaystationTower.LanternSide;
                for (int k = 0; k < a.Count; k++)
                {
                    Assert.AreEqual(a[k].Position, b[k].Position, "the lanterns are not a pure function of the course");
                    foreach (var ring in laid)
                    {
                        Vector3 d = a[k].Position - ring.Position;
                        float along = Vector3.Dot(d, ring.Axis);
                        float radial = (d - ring.Axis * along).magnitude;
                        Assert.IsFalse(Mathf.Abs(along) < pad && radial < ring.Radius + pad,
                            $"i{intensity} seed {seed}: a lantern of ring {a[k].Gate} stands in a ring's mouth");
                    }
                }
                stood += a.Count;
            }
            // At I1 most towers are a single plunge (no hop after it, so no lanterns): count over the seeds.
            Assert.Greater(stood, 0, $"i{intensity}: no lanterns on twenty courses");
            }
        }

        [Test]
        public void CrownGemsOnlyAtI4OneATowerOnThePlungeLine()
        {
            var gems = new List<Vector3>();
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 20; seed++)
            {
                var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                WaystationTower.CrownGems(laid, intensity, gems);
                int plunges = 0;
                for (int i = 1; i < laid.Count; i++)
                {
                    if (!WaystationTower.IsPlunge(laid, i, t)) continue;
                    if (intensity == 4)
                    {
                        float depth = Vector3.Dot(gems[plunges] - WaystationTower.PressOf(laid, i, t), laid[i].Axis);
                        float plunge = WaystationTower.PlungeOf(laid, i, t);
                        Assert.Greater(depth, t.WindowLow + plunge, "a short plunge would not reach the gem");
                        Assert.Less(depth, t.WindowHigh + plunge, "the deepest plunge would not skip the gem");
                    }
                    plunges++;
                }
                Assert.AreEqual(intensity == 4 ? plunges : 0, gems.Count, $"i{intensity} seed {seed}: crown gems");
            }
        }

        [Test]
        public void EachLevelAddsOneThing()
        {
            var i1 = WaystationTowerSettings.ForIntensity(1);
            var i2 = WaystationTowerSettings.ForIntensity(2);
            var i3 = WaystationTowerSettings.ForIntensity(3);
            var i4 = WaystationTowerSettings.ForIntensity(4);
            Assert.AreEqual(0f, i1.ShelfShare, "I1 is every hop loose: hold into the lanterns");
            Assert.Greater(i2.ShelfShare, 0f, "I2 adds tight hops: read the lanterns' length");
            Assert.AreEqual(0f, i2.RollGateShare, "I2 adds only the shelf");
            Assert.Greater(i3.RollGateShare, 0f, "I3 adds roll gates");
            Assert.AreEqual(0f, i3.CrownGem, "I3 has no crown gem");
            Assert.Greater(i4.CrownGem, 0f, "I4 adds the crown gem");
        }

        static string Normalised(string path) =>
            System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void TheCourseIsCutForTheShippedTigerMoth()
        {
            // Line endings normalised: a Windows checkout may carry CRLF, and the Hull pattern spans lines.
            string prefab = Normalised("Assets/_Prefabs/Spacevessels/TigerMoth.prefab");
            string drop = Normalised("Assets/_SO_Assets/VesselActions/TigerMoth/TigerMothDropAction.asset");
            float One(string src, string key)
            {
                var m = System.Text.RegularExpressions.Regex.Match(src, @"^\s*" + key + @":\s*(-?[0-9.]+)\s*$",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{key} not found");
                return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
            Assert.AreEqual(WaystationTower.Cruise, One(prefab, "DefaultMinimumSpeed") + One(prefab, "DefaultThrottleScaler"), 1e-3f);
            Assert.AreEqual(WaystationTower.RollDegreesPerSecond, One(prefab, "RollScaler"), 1e-3f);
            var hull = System.Text.RegularExpressions.Regex.Match(prefab,
                @"m_Name: Hull\n.*?--- !u!135 &\d+\nSphereCollider:.*?m_Radius: ([0-9.]+)",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.IsTrue(hull.Success, "the Hull SphereCollider is not found");
            Assert.AreEqual(WaystationTower.HullRadius,
                float.Parse(hull.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-3f);
            Assert.AreEqual(WaystationTower.DropSpeed, One(drop, "dropSpeed"), 1e-3f);
            Assert.AreEqual(WaystationTower.DropEaseSeconds, One(drop, "dropEaseSeconds"), 1e-4f);
            Assert.AreEqual(WaystationTower.LandSeconds, One(drop, "landSeconds"), 1e-4f);
            Assert.AreEqual(WaystationTower.FullCooldownSeconds, One(drop, "fullCooldownSeconds"), 1e-4f);
            Assert.AreEqual(WaystationTower.FullCooldownDistance, One(drop, "fullCooldownDistance"), 1e-3f);
        }

        [Test]
        public void AStolenTileStandsAndOnlyAGoneTileRegrows()
        {
            Assert.IsTrue(WaystationTower.TileNeedsRegrow(false, false, false, 0f), "never laid");
            Assert.IsTrue(WaystationTower.TileNeedsRegrow(true, true, true, 0f), "destroyed");
            Assert.IsFalse(WaystationTower.TileNeedsRegrow(true, false, true, 0f), "standing, ours");
            Assert.IsFalse(WaystationTower.TileNeedsRegrow(true, false, false, 0f),
                "stolen by the dust, standing in place: still the wall, and a second tile must not be laid on it");
            Assert.IsTrue(WaystationTower.TileNeedsRegrow(true, false, false, 300f),
                "the pooled prism was reused somewhere else: the tile is gone");
        }

        [Test]
        public void WallsAreAPureFunctionOfTheBroadcastCourse()
        {
            var course = Course(3);
            var laid = WaystationTower.Build(11, course, 3, out _);
            var a = new List<WaystationTowerTile>();
            var b = new List<WaystationTowerTile>();
            WaystationTower.Walls(laid, 3, a);
            WaystationTower.Walls(new List<RaceGate>(laid), 3, b);
            Assert.Greater(a.Count, 0, "no walls on a course with towers");
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Danger, b[i].Danger);
            }
        }
    }
}
