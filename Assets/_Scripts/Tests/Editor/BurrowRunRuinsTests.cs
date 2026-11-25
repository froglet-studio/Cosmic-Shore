using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Burrow Run's garden ruins (BURROWRUN.md section 4a): Spearline's generator laid on Burrow Run's own lap
    /// (Slingshot's eight-ring circuit in the barren race shell). Only the hardest rung has them, the lane down every
    /// leg and the space round every ring stay clear of every standing ruin, and a lap carries enough to read.
    /// </summary>
    public class BurrowRunRuinsTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        /// <summary>One lap as <c>BurrowRunCourseSource</c> cuts it (<c>HeadlongCourseSource.BuildCircuit</c>).</summary>
        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = SlingshotCourse.ForIntensity(intensity);
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            settings.GateCount = SlingshotCourse.GatesPerLap;
            return HeadlongCircuit.Generate(seed, settings);
        }

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> lap,
                                             out List<SpearlineRuinWheel> wheels)
        {
            lap = Lap(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), parts, wheels);
            return parts;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        static int RuinsOnLap(List<SpearlineRuinPart> parts)
        {
            int legs = 0, last = -1;
            foreach (var part in parts)
            {
                if (part.Leg != last) legs++;
                last = part.Leg;
            }
            return legs;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _).Count, $"I{i} seed {seed}");
        }

        [Test]
        public void The_hardest_rung_carries_ruins_on_its_long_legs_and_stays_in_budget()
        {
            int total = 0, ruins = 0;
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                int onLap = RuinsOnLap(parts);
                Assert.GreaterOrEqual(onLap, MinRuinsALap, $"seed {seed}: too few ruins a lap");
                Assert.LessOrEqual(onLap, lap.Count - 1, $"seed {seed}: the closing leg carries no ruin");
                Assert.GreaterOrEqual(parts.Count, MinPrismsALap, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(parts.Count, MaxPrismsALap, $"seed {seed}: over the prism budget");
                total += parts.Count;
                ruins += onLap;
            }
            Assert.GreaterOrEqual(ruins / (float)Seeds.Length, MeanRuinsALap, "ruins a lap, mean");
            Assert.GreaterOrEqual(total / (float)Seeds.Length, MeanPrismsALap, "ruin prisms a lap, mean");
        }

        // Measured on these seeds on 2026-11-24: 4 ruins a lap (5 on one seed) and 424 to 546 prisms, mean 450. Four
        // of the seven legs are 562 u or longer; the other three are 344 to 381 u, under the generator's 380 u floor.
        const int MinRuinsALap = 4, MinPrismsALap = 400, MaxPrismsALap = 1000;
        const float MeanRuinsALap = 4f, MeanPrismsALap = 430f;

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(lap, parts[i]), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into a lane");
                    if (parts[i].Scale.z <= 16f) continue;
                    // The ends of a long bar, checked here without the generator's own sampler.
                    Vector3 half = parts[i].Forward * (0.5f * parts[i].Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position + half), SpearlineRuins.LaneRadius - 0.01f);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position - half), SpearlineRuins.LaneRadius - 0.01f);
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_mouth()
        {
            // A pilot who threads a ring is never asked for a narrower pass between rings.
            Assert.Greater(SpearlineRuins.LaneRadius, SlingshotCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                foreach (var part in parts)
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap, part.Position), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void At_most_one_wheel_turns_a_lap_and_its_parts_are_one_ruin()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out _, out var wheels);
                Assert.LessOrEqual(wheels.Count, 1, $"seed {seed}");
                foreach (var wheel in wheels)
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        Assert.AreEqual(parts[wheel.First].Leg, parts[i].Leg);
            }
        }

        [Test]
        public void Every_ruin_prism_is_thick()
        {
            foreach (int seed in Seeds)
                foreach (var part in Ruins(4, seed, out _, out _))
                {
                    Assert.GreaterOrEqual(part.Scale.x, 8f);
                    Assert.GreaterOrEqual(part.Scale.y, 8f);
                }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            foreach (int seed in Seeds)
            {
                var first = Ruins(4, seed, out _, out _);
                var second = Ruins(4, seed, out _, out _);
                Assert.AreEqual(first.Count, second.Count);
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.AreEqual(first[i].Position, second[i].Position);
                    Assert.AreEqual(first[i].Scale, second[i].Scale);
                }
            }
        }
    }
}
