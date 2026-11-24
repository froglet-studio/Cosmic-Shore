using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Spearline's garden ruins (SPEARLINE.md §2a): only the hardest rung has them, the lane down every leg and the
    /// space round every ring stay clear, the lay is a pure function of the lap, and there is enough of each kind to
    /// read as a ruin. The same file runs headless through <c>Tools/Build/spearline_harness</c>.
    /// </summary>
    public class SpearlineRuinsTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var settings = HeadlongCircuitSettings.ForIntensity(intensity);
            settings.RingRadius = SpearlineSeals.ForIntensity(intensity).MouthRadius;
            settings.InnerRadius = 480f;
            settings.OuterRadius = 1080f;
            settings.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, settings);
        }

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> lap)
        {
            lap = Lap(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(intensity), parts);
            return parts;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _).Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed, out _).Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap);
                foreach (var part in parts)
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, part.Position), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism stands in a lane");
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap);
                foreach (var part in parts)
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap, part.Position), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void The_lane_is_at_least_as_wide_as_the_mouth_allows()
        {
            // A pilot who threads a ring on its centre line must not be asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, 0.9f * SpearlineSeals.ForIntensity(4).MouthRadius);
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            foreach (int seed in Seeds)
            {
                var first = Ruins(4, seed, out _);
                var second = Ruins(4, seed, out _);
                Assert.AreEqual(first.Count, second.Count);
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.AreEqual(first[i].Position, second[i].Position);
                    Assert.AreEqual(first[i].Scale, second[i].Scale);
                }
            }
        }

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            foreach (int seed in Seeds)
            {
                int count = Ruins(4, seed, out _).Count;
                Assert.GreaterOrEqual(count, 150, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(count, 700, $"seed {seed}: over the prism budget");
            }
        }

        [Test]
        public void Every_part_is_turned_and_sized()
        {
            foreach (var part in Ruins(4, 1, out _))
            {
                Assert.AreEqual(1f, part.Forward.magnitude, 1e-3f);
                Assert.AreEqual(1f, part.Up.magnitude, 1e-3f);
                Assert.Less(Mathf.Abs(Vector3.Dot(part.Forward, part.Up)), 0.6f, "forward and up nearly parallel");
                Assert.Greater(part.Scale.x, 0f);
                Assert.Greater(part.Scale.z, 0f);
            }
        }

        [Test]
        public void All_three_kinds_appear_over_a_handful_of_courses()
        {
            var seen = new bool[SpearlineRuins.KindCount];
            foreach (int seed in Seeds)
            {
                var lap = Lap(4, seed);
                for (int leg = 0; leg < lap.Count - 1; leg++)
                    seen[SpearlineRuins.KindOf(leg, lap[leg].Position)] = true;
            }
            foreach (bool kind in seen) Assert.IsTrue(kind);
        }
    }
}
