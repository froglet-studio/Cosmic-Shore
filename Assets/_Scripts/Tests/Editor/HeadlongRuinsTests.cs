using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Headlong's I4 garden ruins (<see cref="HeadlongRuins"/>, HEADLONG.md §0a) on the shipped circuit, 400 seeds: only
    /// the hardest rung has them, the lane down every leg and the space round every gate stay clear of every standing
    /// ruin, no ruin prism stands in a hedge or on a Time crystal, a wheel is laid whole and turns through neither, and
    /// there is enough left of the ruins to read. Runs offline in Tools/Build/headlong_course_harness.
    /// </summary>
    public class HeadlongRuinsTests
    {
        const int Seeds = 400;

        sealed class Lap
        {
            public List<RaceGate> Gates;
            public readonly List<HeadlongHedges.Hedge> Hedges = new();
            public readonly List<Vector3> Crystals = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Laid = new();

            public int Count
            {
                get
                {
                    int n = 0;
                    foreach (bool laid in Laid) if (laid) n++;
                    return n;
                }
            }

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }
        }

        static Lap Ruins(int intensity, int seed)
        {
            var s = HeadlongCircuitSettings.ForIntensity(intensity);
            s.InnerRadius = 480f;
            s.OuterRadius = 1080f;
            s.FirstGateDirection = Vector3.up;
            var lap = new Lap { Gates = HeadlongCircuit.Generate(seed, s) };
            HeadlongHedges.Place(lap.Gates, intensity, lap.Hedges);
            HeadlongHedges.PlaceCrystals(lap.Gates, intensity, lap.Hedges, lap.Crystals);
            HeadlongRuins.Lay(lap.Gates, intensity, lap.Hedges, lap.Crystals, lap.Parts, lap.Wheels, lap.Laid);
            return lap;
        }

        [Test]
        public void Only_the_hardest_level_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var below = Ruins(i, seed);
                    Assert.AreEqual(0, below.Parts.Count, $"I{i} seed {seed}");
                    Assert.AreEqual(0, below.Wheels.Count, $"I{i} seed {seed}");
                }
                Assert.Greater(Ruins(4, seed).Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Ruins(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i] || lap.Turns(i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(lap.Gates, lap.Parts[i]),
                        SpearlineRuins.LaneRadius - 0.01f, $"seed {seed}: a ruin prism reaches into a lane");
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_gate()
        {
            // A pilot who threads a gate on its centre line is not asked for a narrower pass between gates.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, HeadlongCircuitSettings.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_gate()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Ruins(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i]) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap.Gates, lap.Parts[i].Position),
                        SpearlineRuins.RingMargin - 0.01f, $"seed {seed}: a ruin prism stands at a gate");
                }
            }
        }

        [Test]
        public void No_ruin_stands_in_a_hedge_or_on_a_crystal()
        {
            int leftOut = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Ruins(4, seed);
                for (int i = 0; i < lap.Parts.Count; i++)
                {
                    if (!lap.Laid[i]) { leftOut++; continue; }
                    if (lap.Turns(i)) continue;
                    var part = lap.Parts[i];
                    Assert.GreaterOrEqual(HeadlongRuins.Clearance(part, lap.Hedges, lap.Crystals), 0f, $"seed {seed}");
                    // The prism's centre and both its ends, checked here without the generator's own sampler.
                    Vector3 half = part.Forward * (0.5f * part.Scale.z);
                    foreach (var p in new[] { part.Position, part.Position + half, part.Position - half })
                    {
                        foreach (var hedge in lap.Hedges)
                        {
                            float along = Vector3.Dot(p - hedge.Centre, hedge.Normal);
                            float radial = (p - hedge.Centre - hedge.Normal * along).magnitude;
                            Assert.IsTrue(Mathf.Abs(along) >= HeadlongRuins.HedgeGap || radial >= hedge.Radius + HeadlongRuins.HedgeGap,
                                $"seed {seed}: a ruin prism stands in the hedge on leg {hedge.Leg}");
                        }
                        foreach (var crystal in lap.Crystals)
                            Assert.GreaterOrEqual((p - crystal).magnitude, HeadlongRuins.CrystalGap, $"seed {seed}: a ruin prism on a crystal");
                    }
                }
            }
            // The rule does something: on the shipped circuit a ruin and a hedge do share legs.
            Assert.Greater(leftOut, Seeds * 20, "no ruin prism was ever left out for a hedge");
        }

        [Test]
        public void A_wheel_is_laid_whole_and_turns_through_no_hedge_or_crystal()
        {
            int courses = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Ruins(4, seed);
                Assert.LessOrEqual(lap.Wheels.Count, 1, $"seed {seed}");
                foreach (var wheel in lap.Wheels)
                {
                    courses++;
                    Assert.IsFalse(HeadlongRuins.WheelMeets(wheel, lap.Hedges, lap.Crystals), $"seed {seed}");
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                    {
                        Assert.IsTrue(lap.Laid[i], $"seed {seed}: a wheel with a stone left out");
                        foreach (float seconds in new[] { 0f, 1.7f, 4.1f, 9.3f })
                        {
                            SpearlineRuins.Pose(lap.Parts[i], wheel, seconds, out var position, out _, out _);
                            foreach (var hedge in lap.Hedges)
                            {
                                float along = Vector3.Dot(position - hedge.Centre, hedge.Normal);
                                float radial = (position - hedge.Centre - hedge.Normal * along).magnitude;
                                Assert.IsTrue(Mathf.Abs(along) >= HeadlongRuins.HedgeGap || radial >= hedge.Radius + HeadlongRuins.HedgeGap,
                                    $"seed {seed}: the wheel turns through the hedge on leg {hedge.Leg} at {seconds} s");
                            }
                        }
                    }
                }
            }
            // Most legs carry a hedge at I4, so the wheel is the rare ruin here: about one course in six.
            Assert.GreaterOrEqual(courses, Seeds / 10, "the wheel turns on too few courses");
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            var counts = new List<int>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var lap = Ruins(4, seed);
                int count = lap.Count;
                counts.Add(count);
                Assert.GreaterOrEqual(count, 450, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(count, 1000, $"seed {seed}: over the prism budget");
                var legs = new HashSet<int>();
                for (int i = 0; i < lap.Parts.Count; i++)
                    if (lap.Laid[i]) legs.Add(lap.Parts[i].Leg);
                Assert.GreaterOrEqual(legs.Count, 5, $"seed {seed}: too few ruins a lap");
            }
            counts.Sort();
            Assert.GreaterOrEqual(counts[Seeds / 2], 600, "the median lap");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var first = Ruins(4, seed);
                var second = Ruins(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                Assert.AreEqual(first.Wheels.Count, second.Wheels.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Laid[i], second.Laid[i]);
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                }
            }
        }
    }
}
