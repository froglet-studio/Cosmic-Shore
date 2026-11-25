using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Serpentine's garden ruins (SERPENTTIME.md, "Garden ruins at I4"): Spearline's generator (<see cref="SpearlineRuins"/>)
    /// called on THIS mode's lap (<see cref="SerpentTimeCourse"/>, the shell <c>SerpentTimeCourseTests</c> uses). Held
    /// here: only the hardest rung has them, the lane down every leg stays clear of every standing ruin, nothing stands
    /// near a ring or inside the shape that closes it, the leg the field starts on carries none, and a lap carries
    /// enough to read while staying in budget. The generator's own rules (the kinds, the wheel) are
    /// <c>SpearlineRuinsTests</c>'. The same file runs headless through <c>Tools/Build/serpent_time_course_harness</c>.
    /// </summary>
    public class SerpentineRuinsTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 41, 42, 101, 102, 103, 20365644, 20470373 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = SerpentTimeCourse.ForIntensity(intensity);
            settings.InnerRadius = 480f;
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
                    // The ends of a long bar, checked here without the generator's own sampler.
                    if (parts[i].Scale.z <= 16f) continue;
                    Vector3 half = parts[i].Forward * (0.5f * parts[i].Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position + half), SpearlineRuins.LaneRadius - 0.01f);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position - half), SpearlineRuins.LaneRadius - 0.01f);
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_ring_it_leads_to()
        {
            // A pilot who threads a ring is never asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, SerpentTimeCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_ring_or_inside_its_shape()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                foreach (var part in parts)
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap, part.Position), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
            }
            // The shape that closes a ring (SerpentineGate) reaches less far than the margin the ruins keep, with room
            // for a ruin prism's own half length (the longest is a maze bar).
            float reach = SerpentineGate.OuterReach(SerpentTimeCourse.ForIntensity(4).RingRadius, 4);
            Assert.LessOrEqual(reach + 20f, SpearlineRuins.RingMargin, $"the I4 shape reaches {reach} u");
        }

        [Test]
        public void The_leg_the_field_starts_on_carries_no_ruin()
        {
            // The start ring stands behind gate 0, on the leg that closes the lap.
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                foreach (var part in parts)
                    Assert.Less(part.Leg, lap.Count - 1, $"seed {seed}: a ruin on the closing leg");
            }
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            int total = 0, legsTotal = 0;
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out _, out var wheels);
                int legs = 0, last = -1;
                foreach (var part in parts)
                {
                    if (part.Leg != last) legs++;
                    last = part.Leg;
                }
                total += parts.Count;
                legsTotal += legs;
                Assert.GreaterOrEqual(legs, MinRuins, $"seed {seed}: {legs} ruins");
                Assert.GreaterOrEqual(parts.Count, MinPrisms, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(parts.Count, MaxPrisms, $"seed {seed}: over the prism budget");
                Assert.LessOrEqual(wheels.Count, 1, $"seed {seed}: more than one wheel");
            }
            float mean = total / (float)Seeds.Length;
            Assert.GreaterOrEqual(mean, MinMeanPrisms, $"mean {mean} prisms, {legsTotal / (float)Seeds.Length} ruins");
        }

        const int MinRuins = 6, MinPrisms = 650, MaxPrisms = 1000;
        const float MinMeanPrisms = 750f;

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
