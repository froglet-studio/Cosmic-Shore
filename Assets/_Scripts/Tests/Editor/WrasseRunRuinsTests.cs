using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Wrasse Run's garden ruins (WRASSERUN.md §3a): Spearline's generator (<see cref="SpearlineRuins"/>) laid round the
    /// WRASSE's lap (<see cref="WrasseRunCourse.ForIntensity"/> on the shared Headlong solver). Only I4 has them, the
    /// 60 u lane down every leg and the 120 u round every ring stay clear of every standing ruin, a lap carries enough
    /// to read and stays in the prism budget, and the lane is wider than the kite's mouth. The generator's own shape
    /// rules (thickness, the wheel) are held by <c>SpearlineRuinsTests</c>.
    /// </summary>
    public class WrasseRunRuinsTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = WrasseRunCourse.ForIntensity(intensity);
            settings.InnerRadius = 480f;    // the barren race cell's shell (WrasseRunCourseTests)
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
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

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _).Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed, out _, out _).Count, 0, $"I4 seed {seed}");
            }
        }

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
        public void The_lane_is_wider_than_every_mouth()
        {
            // The kite threads the mouth; the hull that carries it is never asked for a narrower pass between rings.
            for (int i = 1; i <= 4; i++)
                Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, WrasseRunCourse.ForIntensity(i).RingRadius, $"I{i}");
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                Assert.GreaterOrEqual(parts.Count, 300, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(parts.Count, 1000, $"seed {seed}: over the prism budget");
                Assert.LessOrEqual(wheels.Count, 1, $"seed {seed}: more than one wheel a lap");

                int legs = 0, last = -1;
                foreach (var part in parts)
                {
                    Assert.Less(part.Leg, lap.Count - 1, "the closing leg carries no ruin");
                    if (part.Leg != last) legs++;
                    last = part.Leg;
                }
                Assert.GreaterOrEqual(legs, 4, $"seed {seed}: too few legs carry a ruin");
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
