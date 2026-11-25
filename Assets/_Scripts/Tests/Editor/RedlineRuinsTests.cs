using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Redline's garden ruins (REDLINE.md section 4b): Spearline's ruin kinds on Redline's own lap. Only I4 has them, the
    /// 60 u lane down every leg and the space round every gate stay clear of every standing ruin, nothing stands on a
    /// Time crystal or in the nucleus, a wheel sweeps no crystal, and there is enough ruin left to read.
    /// Runs headless through <c>Tools/Build/redline_course_harness</c>.
    /// </summary>
    public class RedlineRuinsTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Seeds = 60;

        sealed class Laid
        {
            public List<RaceGate> Lap;
            public readonly List<Vector3> Crystals = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public readonly List<bool> Hidden = new();

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }

            public int Standing()
            {
                int n = 0;
                foreach (bool hidden in Hidden) if (!hidden) n++;
                return n;
            }
        }

        static Laid Ruins(int intensity, int seed)
        {
            var s = RedlineCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            var laid = new Laid { Lap = HeadlongCircuit.Generate(seed, s) };
            RedlineTimeCrystals.Place(laid.Lap, intensity, laid.Crystals);
            RedlineRuins.Lay(laid.Lap, intensity, laid.Crystals, laid.Parts, laid.Wheels, laid.Hidden);
            return laid;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed).Parts.Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed).Standing(), 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void Every_part_has_its_flag()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                Assert.AreEqual(laid.Parts.Count, laid.Hidden.Count, $"seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Hidden[i] || laid.Turns(i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(laid.Lap, laid.Parts[i]),
                        SpearlineRuins.LaneRadius - 0.01f, $"seed {seed}: a ruin prism reaches into a lane");
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_mouth()
        {
            // A pilot who threads a 44 u mouth must not be asked for a narrower pass between gates.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, RedlineCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_gate()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Hidden[i]) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(laid.Lap, laid.Parts[i].Position),
                        SpearlineRuins.RingMargin - 0.01f, $"seed {seed}: a ruin prism stands at a gate");
                }
            }
        }

        [Test]
        public void No_ruin_stands_on_a_time_crystal_or_in_the_nucleus()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                Assert.Greater(laid.Crystals.Count, 0, $"seed {seed}: I4 has Time crystals");
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Hidden[i]) continue;
                    var part = laid.Parts[i];
                    if (laid.Turns(i)) continue;   // judged by its wheel's sweep, below
                    Assert.GreaterOrEqual(RedlineRuins.CrystalDistance(laid.Crystals, part.Position),
                        RedlineRuins.CrystalClearance, $"seed {seed}: a ruin prism stands on a Time crystal");
                    Assert.GreaterOrEqual(part.Position.magnitude, RedlineRuins.NucleusClearance,
                        $"seed {seed}: a ruin prism stands in the nucleus");
                }
            }
        }

        [Test]
        public void A_standing_wheel_sweeps_no_time_crystal_and_stands_whole()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                foreach (var wheel in laid.Wheels)
                {
                    bool hidden = laid.Hidden[wheel.First];
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        Assert.AreEqual(hidden, laid.Hidden[i], $"seed {seed}: a wheel is laid in part");
                    if (hidden) continue;
                    Assert.GreaterOrEqual(RedlineRuins.CrystalDistance(laid.Crystals, wheel.Centre),
                        SpearlineRuins.WheelRadius + RedlineRuins.CrystalClearance, $"seed {seed}: a wheel sweeps a Time crystal");
                }
            }
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            int total = 0, legsTotal = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Ruins(4, seed);
                int count = laid.Standing();
                total += count;
                var legs = new HashSet<int>();
                for (int i = 0; i < laid.Parts.Count; i++)
                    if (!laid.Hidden[i]) legs.Add(laid.Parts[i].Leg);
                legsTotal += legs.Count;
                Assert.GreaterOrEqual(legs.Count, 4, $"seed {seed}: too few legs carry a ruin");
                Assert.GreaterOrEqual(count, 600, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(count, 1000, $"seed {seed}: over the prism budget");
            }
            Assert.GreaterOrEqual(total / (float)Seeds, 700f, "mean prisms a lap");
            Assert.GreaterOrEqual(legsTotal / (float)Seeds, 6.5f, "mean legs with a ruin");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var first = Ruins(4, seed);
                var second = Ruins(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Hidden[i], second.Hidden[i]);
                }
            }
        }
    }
}
