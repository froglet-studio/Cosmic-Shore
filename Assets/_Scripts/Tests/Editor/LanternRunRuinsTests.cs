using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Lantern Run's garden ruins (LANTERNRUN.md, "The ruins"): Spearline's ruins laid from Lantern Run's rings. Only
    /// the hardest rung has them; the lane down every leg and the space round every ring stay clear of every standing
    /// ruin; no ruin stands in (and no wheel sweeps) a prism mass cluster; a wheel is laid whole or not at all; the lay
    /// is a pure function of the rings; and there is enough left to read as ruins. The same file runs headless through
    /// <c>Tools/Build/lantern_run_harness</c>.
    /// </summary>
    public class LanternRunRuinsTests
    {
        const int Seeds = 60;

        sealed class Lap
        {
            public List<RaceGate> Rings;
            public List<LanternRunCourse.Cluster> Clusters;
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Laid = new();

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }

            public int LaidCount()
            {
                int n = 0;
                foreach (bool laid in Laid) if (laid) n++;
                return n;
            }

            public int LaidLegs()
            {
                var legs = new HashSet<int>();
                for (int i = 0; i < Parts.Count; i++) if (Laid[i]) legs.Add(Parts[i].Leg);
                return legs.Count;
            }
        }

        static Lap Build(int intensity, int seed)
        {
            var settings = LanternRunCourse.ForIntensity(intensity);
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            var lap = new Lap { Rings = HeadlongCircuit.Generate(seed, settings) };
            lap.Clusters = LanternRunCourse.Clusters(lap.Rings, intensity);
            LanternRunRuins.Lay(lap.Rings, intensity, lap.Clusters, lap.Parts, lap.Wheels, lap.Laid);
            return lap;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The nearest any cluster prism's centre comes to the part's surface (its centre line less half its
        /// thickness), with the part standing at <paramref name="position"/> along <paramref name="forward"/>.</summary>
        static float SlotGap(Lap lap, in SpearlineRuinPart part, Vector3 position, Vector3 forward)
        {
            Vector3 half = forward * (0.5f * part.Scale.z);
            float thick = 0.5f * Mathf.Max(part.Scale.x, part.Scale.y);
            float best = float.MaxValue;
            foreach (var cluster in lap.Clusters)
                foreach (var slot in cluster.Slots)
                    best = Mathf.Min(best, SegmentDistance(slot, position - half, position + half) - thick);
            return best;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var lap = Build(i, seed);
                    Assert.AreEqual(0, lap.Parts.Count, $"I{i} seed {seed}");
                    Assert.AreEqual(0, lap.Wheels.Count, $"I{i} seed {seed}");
                }
                Assert.Greater(Build(4, seed).LaidCount(), 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i] || lap.Turns(i)) continue;
                    var part = lap.Parts[i];
                    // The middle, and the ends of a long bar, without the generator's own sampler.
                    Vector3 half = part.Forward * (0.5f * part.Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap.Rings, part.Position), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism stands in a lane");
                    // A short stone is placed by its centre (SpearlineRuins.Stands), as in Spearline.
                    if (part.Scale.z <= 16f) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap.Rings, part.Position + half), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into a lane");
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap.Rings, part.Position - half), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into a lane");
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i]) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap.Rings, lap.Parts[i].Position),
                        SpearlineRuins.RingMargin - 0.01f, $"seed {seed}: a ruin prism stands at a ring");
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_tightest_mouth()
        {
            // A pilot who threads a ring must not be asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, LanternRunCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_standing_ruin_reaches_a_cluster()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i] || lap.Turns(i)) continue;
                    Assert.GreaterOrEqual(SlotGap(lap, lap.Parts[i], lap.Parts[i].Position, lap.Parts[i].Forward),
                        LanternRunRuins.ClusterKeep - 0.01f, $"seed {seed}: a ruin prism stands in a cluster");
                }
            }
        }

        [Test]
        public void A_wheel_is_whole_or_absent_and_never_sweeps_a_cluster()
        {
            int wheels = 0, turning = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                foreach (var wheel in lap.Wheels)
                {
                    wheels++;
                    bool first = lap.Laid[wheel.First];
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        Assert.AreEqual(first, lap.Laid[i], $"seed {seed}: a wheel laid in part");
                    if (!first) continue;
                    turning++;
                    // A whole turn at 25 deg/s is 14.4 s.
                    for (float seconds = 0f; seconds < 14.4f; seconds += 0.3f)
                    {
                        for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        {
                            SpearlineRuins.Pose(lap.Parts[i], wheel, seconds, out var position, out var forward, out _);
                            Assert.Greater(SlotGap(lap, lap.Parts[i], position, forward), 0f,
                                $"seed {seed}: a wheel sweeps a cluster at {seconds} s");
                        }
                    }
                }
            }
            Assert.Greater(wheels, 0, "no lap had a wheel");
            Assert.GreaterOrEqual(turning, wheels / 2, "the clusters cost more than half the wheels");
        }

        [Test]
        public void A_cluster_in_a_wheels_sweep_takes_the_whole_wheel_out()
        {
            // No shipped lap puts a cluster in a wheel's sweep (measured, seeds 1-60), so the rule is held with a
            // cluster stood where a rim stone will be a third of a turn on.
            int tried = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                if (lap.Wheels.Count == 0) continue;
                tried++;
                var wheel = lap.Wheels[0];
                int rim = wheel.First + wheel.Count - 1;
                SpearlineRuins.Pose(lap.Parts[rim], wheel, 120f / Mathf.Abs(wheel.DegreesPerSecond),
                                    out var where, out _, out _);
                var clusters = new List<LanternRunCourse.Cluster>(lap.Clusters)
                {
                    new LanternRunCourse.Cluster(0, LanternRunCourse.ClusterRole.Near, where, new[] { where }),
                };
                var parts = new List<SpearlineRuinPart>();
                var wheels = new List<SpearlineRuinWheel>();
                var laid = new List<bool>();
                LanternRunRuins.Lay(lap.Rings, 4, clusters, parts, wheels, laid);
                for (int i = wheels[0].First; i < wheels[0].First + wheels[0].Count; i++)
                    Assert.IsFalse(laid[i], $"seed {seed}: a wheel turns through a cluster");
            }
            Assert.Greater(tried, 0, "no lap had a wheel");
        }

        [Test]
        public void The_clusters_are_what_they_were_without_ruins()
        {
            // The ruins are laid round the clusters, never the other way: Clusters takes no ruin.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                var plain = LanternRunCourse.Clusters(lap.Rings, 4);
                Assert.AreEqual(plain.Count, lap.Clusters.Count);
                for (int c = 0; c < plain.Count; c++)
                    Assert.AreEqual(LanternRunCourse.ClusterPrisms(4), plain[c].Slots.Length,
                        $"seed {seed}: cluster {c} lost a prism");
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_rings()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var first = Build(4, seed);
                var second = Build(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                Assert.AreEqual(first.Wheels.Count, second.Wheels.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Parts[i].Scale, second.Parts[i].Scale);
                    Assert.AreEqual(first.Laid[i], second.Laid[i]);
                }
            }
        }

        [Test]
        public void There_is_enough_left_to_read_as_ruins()
        {
            int legs = 0, prisms = 0, kept = 0, made = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Build(4, seed);
                Assert.GreaterOrEqual(lap.LaidLegs(), MinLegsALap, $"seed {seed}: too few legs carry a ruin");
                Assert.GreaterOrEqual(lap.LaidCount(), MinPrismsALap, $"seed {seed}: too few ruin prisms");
                foreach (var part in lap.Parts)
                    Assert.GreaterOrEqual(Mathf.Min(part.Scale.x, part.Scale.y), 8f,
                        $"seed {seed}: a thin ruin prism");
                legs += lap.LaidLegs();
                prisms += lap.LaidCount();
                kept += lap.LaidCount();
                made += lap.Parts.Count;
            }
            // The clusters take a few prisms, not the ruins.
            Assert.GreaterOrEqual(kept, 0.8f * made, "the clusters cost more than a fifth of the ruin prisms");
            Assert.GreaterOrEqual(legs, MeanLegsALap * Seeds, "too few legs carry a ruin on average");
        }

        // Measured over seeds 1-60 by Tools/Build/lantern_run_harness --measure (2026-11-24): 7 legs on every lap,
        // 734-873 prisms a lap (mean 796), 35 of 47,812 prisms left out for a cluster, every wheel kept (30 of 30).
        const int MinLegsALap = 7;
        const int MinPrismsALap = 600;
        const float MeanLegsALap = 7f;
    }
}
