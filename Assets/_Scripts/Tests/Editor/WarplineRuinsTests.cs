using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Warpline's garden ruins (WARPLINE.md §2a): Spearline's generator laid on Warpline's own lap (Slingshot's circuit
    /// cut into five long legs). Only the hardest rung has them, the lane down every leg and the space round every ring
    /// stay clear of every STANDING ruin, there is one ruin a leg at most and none on the leg that closes the lap, and
    /// the lay is a pure function of the lap.
    /// </summary>
    public class WarplineRuinsTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        /// <summary>Rings a lap: the shipped target (10) over the shipped laps (2).</summary>
        const int RingsPerLap = 5;

        /// <summary>One lap as WarplineCourseSource builds it (HeadlongCourseSource.BuildCircuit), in the scene's shell.</summary>
        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = SlingshotCourse.ForIntensity(intensity);
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            settings.GateCount = RingsPerLap;
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
        public void The_lap_is_five_rings()
        {
            foreach (int seed in Seeds)
                Assert.AreEqual(RingsPerLap, Lap(4, seed).Count, $"seed {seed}");
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
                    // The ends of a long bar, checked here without the generator's own sampler.
                    if (parts[i].Scale.z <= 16f) continue;
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
        public void One_ruin_a_leg_at_most_and_none_on_the_closing_leg()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                var legs = new HashSet<int>();
                foreach (var part in parts)
                {
                    Assert.Less(part.Leg, lap.Count - 1, $"seed {seed}: a ruin on the leg that closes the lap");
                    legs.Add(part.Leg);
                }
                // Five rings give four legs that may carry a ruin; most of them do.
                Assert.GreaterOrEqual(legs.Count, 2, $"seed {seed}: too few ruins to read");
                Assert.LessOrEqual(legs.Count, RingsPerLap - 1, $"seed {seed}");
            }
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            foreach (int seed in Seeds)
            {
                int count = Ruins(4, seed, out _, out _).Count;
                Assert.GreaterOrEqual(count, 300, $"seed {seed}: too little to read as ruins");
                // Spearline's seven-leg lap is held under 1000; four legs stay under the same ceiling.
                Assert.LessOrEqual(count, 1000, $"seed {seed}: over the prism budget");
            }
        }

        [Test]
        public void No_more_than_one_wheel_turns_a_lap()
        {
            foreach (int seed in Seeds)
            {
                Ruins(4, seed, out _, out var wheels);
                Assert.LessOrEqual(wheels.Count, 1, $"seed {seed}");
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            foreach (int seed in Seeds)
            {
                var first = Ruins(4, seed, out _, out var firstWheels);
                var second = Ruins(4, seed, out _, out var secondWheels);
                Assert.AreEqual(first.Count, second.Count);
                Assert.AreEqual(firstWheels.Count, secondWheels.Count);
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.AreEqual(first[i].Position, second[i].Position);
                    Assert.AreEqual(first[i].Scale, second[i].Scale);
                }
            }
        }
    }
}
