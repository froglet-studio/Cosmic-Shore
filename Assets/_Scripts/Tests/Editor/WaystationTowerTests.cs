using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// WAYSTATION PROTOTYPE's Drop Towers (<see cref="WaystationTower"/>, Docs/Studios/TIGER_MOTH_TIME.md). The full
    /// verdicts - every hop's window, runway, corporeal clearance, the cell, the long last gap - are
    /// <c>Tools/Build/waystation_prototype_course.py --check</c>, which mirrors this class over 200 seeds x 4
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
