using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Thresher Time's garden ruins (THRESHERTIME.md §0b): only the Belfry's hardest rung has them; the lane between
    /// ANY two poses of a leg's swinging rings is clear of every standing ruin; no swung ring reaches a ruin; no ruin
    /// stands, and no wheel turns, where a mace swings; the lay is a pure function of the lap. Spearline's generator is
    /// called, never copied, so its own tests (<c>SpearlineRuinsTests</c>) hold the shapes.
    /// </summary>
    public class ThresherTimeRuinsTests
    {
        const float Inner = 480f, Outer = 1080f;   // the race cell's shell (ThresherTimeCourseTests)
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103, 7001, 7002 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var s = ThresherTimeCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        sealed class Laid
        {
            public List<RaceGate> Lap;
            public readonly ThresherBelfry.Layout Belfry = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Stands = new();

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }

            public int Standing
            {
                get { int n = 0; foreach (bool s in Stands) if (s) n++; return n; }
            }
        }

        static Laid Ruins(int intensity, int seed)
        {
            var laid = new Laid { Lap = Lap(intensity, seed) };
            ThresherBelfry.Build(laid.Lap, intensity, laid.Belfry);
            ThresherTimeRuins.Lay(laid.Lap, intensity, laid.Belfry, laid.Parts, laid.Wheels, laid.Stands);
            return laid;
        }

        static IEnumerable<Vector3> Along(SpearlineRuinPart part)
        {
            int samples = part.Scale.z <= 16f ? 1 : 1 + Mathf.CeilToInt(part.Scale.z / 10f);
            for (int k = 0; k < samples; k++)
                yield return samples == 1
                    ? part.Position
                    : part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed).Parts.Count, $"I{i} seed {seed}");
                var laid = Ruins(4, seed);
                Assert.AreEqual(laid.Parts.Count, laid.Stands.Count, $"seed {seed}: one verdict a part");
                Assert.Greater(laid.Standing, 150, $"I4 seed {seed}: enough standing prisms to read as ruins");
            }
        }

        [Test]
        public void Several_legs_carry_a_ruin_and_the_closing_leg_none()
        {
            foreach (int seed in Seeds)
            {
                var laid = Ruins(4, seed);
                var legs = new HashSet<int>();
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (!laid.Stands[i]) continue;
                    legs.Add(laid.Parts[i].Leg);
                }
                Assert.GreaterOrEqual(legs.Count, 3, $"seed {seed}: ruins on at least three legs");
                Assert.LessOrEqual(legs.Count, ThresherTimeCourse.GatesPerLap - 1, $"seed {seed}: the closing leg carries none");
            }
        }

        [Test]
        public void The_lane_between_any_poses_of_the_swinging_rings_is_clear_of_every_standing_ruin()
        {
            float period = ThresherBelfry.SpecFor(4).RingPeriod;
            foreach (int seed in Seeds)
            {
                var laid = Ruins(4, seed);
                var swung = new List<RaceGate>(laid.Lap.Count);
                for (int step = 0; step < 10; step++)
                {
                    float t = period * step / 10f + seed;
                    swung.Clear();
                    for (int g = 0; g < laid.Lap.Count; g++) swung.Add(ThresherBelfry.GateAt(laid.Belfry, g, t));
                    for (int i = 0; i < laid.Parts.Count; i++)
                    {
                        if (!laid.Stands[i] || laid.Turns(i)) continue;
                        float lane = SpearlineRuins.PartLaneDistance(swung, laid.Parts[i]);
                        Assert.GreaterOrEqual(lane, laid.Lap[0].Radius - 1f, $"seed {seed} part {i} t {t:F1}: {lane:F1} u from a live line");
                    }
                }
                // And Spearline's own lane round the rest line, which is the generator's promise.
                for (int i = 0; i < laid.Parts.Count; i++)
                    if (laid.Stands[i] && !laid.Turns(i))
                        Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(laid.Lap, laid.Parts[i]), SpearlineRuins.LaneRadius - 0.5f, $"seed {seed} part {i}");
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring_at_rest_or_swung()
        {
            float period = ThresherBelfry.SpecFor(4).RingPeriod;
            foreach (int seed in Seeds)
            {
                var laid = Ruins(4, seed);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (!laid.Stands[i] || laid.Turns(i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(laid.Lap, laid.Parts[i]),
                                          SpearlineRuins.RingMargin + ThresherTimeRuins.SwingAllowance - 0.5f, $"seed {seed} part {i} at rest");
                }
                var swung = new List<RaceGate>(laid.Lap.Count);
                for (int step = 0; step < 10; step++)
                {
                    float t = period * step / 10f;
                    swung.Clear();
                    for (int g = 0; g < laid.Lap.Count; g++) swung.Add(ThresherBelfry.GateAt(laid.Belfry, g, t));
                    for (int i = 0; i < laid.Parts.Count; i++)
                        if (laid.Stands[i] && !laid.Turns(i))
                            Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(swung, laid.Parts[i]), 40f, $"seed {seed} part {i} t {t:F1}");
                }
            }
        }

        [Test]
        public void No_standing_ruin_and_no_wheel_is_where_a_mace_swings()
        {
            float period = ThresherBelfry.SpecFor(4).MacePeriod;
            float reach = ThresherBelfry.HeadReach;
            foreach (int seed in Seeds)
            {
                var laid = Ruins(4, seed);
                Assert.Greater(laid.Belfry.Maces.Count, 0, "I4 has maces");
                for (int step = 0; step < 16; step++)
                {
                    float t = period * step / 16f;
                    foreach (var mace in laid.Belfry.Maces)
                    {
                        Vector3 bob = mace.BobAt(t);
                        for (int i = 0; i < laid.Parts.Count; i++)
                        {
                            if (!laid.Stands[i]) continue;
                            var part = laid.Parts[i];
                            float half = Mathf.Max(part.Scale.x, part.Scale.y) * 0.5f;
                            if (laid.Turns(i))
                            {
                                // A wheel is tested at every pose of its turn: the two clocks are not in step.
                                var wheel = WheelOf(laid, i);
                                for (int k = 0; k < 12; k++)
                                {
                                    SpearlineRuins.Pose(part, wheel, k * 1.2f, out var position, out var forward, out var up);
                                    var posed = new SpearlineRuinPart(position, forward, up, part.Scale, part.Leg);
                                    foreach (var p in Along(posed))
                                        Assert.Greater((p - bob).magnitude, reach + half, $"seed {seed} wheel part {i}");
                                }
                                continue;
                            }
                            foreach (var p in Along(part))
                                Assert.Greater((p - bob).magnitude, reach + half, $"seed {seed} part {i} t {t:F1}");
                        }
                    }
                }
            }
        }

        static SpearlineRuinWheel WheelOf(Laid laid, int part)
        {
            foreach (var wheel in laid.Wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return wheel;
            return default;
        }

        [Test]
        public void The_mace_filter_takes_something_out_and_a_course_with_no_clockwork_keeps_everything()
        {
            int dropped = 0;
            foreach (int seed in Seeds)
            {
                var laid = Ruins(4, seed);
                dropped += laid.Parts.Count - laid.Standing;

                var parts = new List<SpearlineRuinPart>();
                var wheels = new List<SpearlineRuinWheel>();
                var stands = new List<bool>();
                ThresherTimeRuins.Lay(laid.Lap, 4, null, parts, wheels, stands);
                Assert.Greater(parts.Count, 0, $"seed {seed}");
                Assert.IsFalse(stands.Contains(false), $"seed {seed}: nothing to keep out of");
            }
            // The negative control of the test above: with the filter doing nothing it would not be testing anything.
            Assert.Greater(dropped, 0, "across the seeds some ruin prism stood in a mace's sweep and was left out");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            var a = Ruins(4, 42);
            var b = Ruins(4, 42);
            Assert.AreEqual(a.Parts.Count, b.Parts.Count);
            for (int i = 0; i < a.Parts.Count; i++)
            {
                Assert.AreEqual(a.Parts[i].Position, b.Parts[i].Position);
                Assert.AreEqual(a.Parts[i].Scale, b.Parts[i].Scale);
                Assert.AreEqual(a.Stands[i], b.Stands[i]);
            }
        }
    }
}
