using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Slingshot's garden ruins (<see cref="SlingshotRuins"/>, <c>Arcade/SLINGSHOT.md</c> "The ruins") on the shipped
    /// Slingshot circuits: only the hardest rung has them, the lane down every leg and the space round every ring stay
    /// clear of every standing ruin, every prism is thick, and the lay is a pure function of the lap. The generator is
    /// Spearline's (<c>SpearlineRuinsTests</c> holds its kinds); this holds it on the Stoat's course.
    /// The same file runs headless through <c>Tools/Build/slingshot_ruins_harness</c>.
    /// </summary>
    public class SlingshotRuinsTests
    {
        const float Inner = 480f;
        const float Outer = 1080f;
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 17, 23, 42, 101, 102, 103, 104, 2026, 90210 };

        static List<RaceGate> Lap(int intensity, int seed)
        {
            var s = SlingshotCourse.ForIntensity(intensity);
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> lap,
                                             out List<SpearlineRuinWheel> wheels)
        {
            lap = Lap(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            SlingshotRuins.Lay(lap, intensity, parts, wheels);
            return parts;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        static int RuinCount(List<SpearlineRuinPart> parts)
        {
            var legs = new HashSet<int>();
            foreach (var part in parts) legs.Add(part.Leg);
            return legs.Count;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            foreach (int seed in Seeds)
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _).Count, $"I{i} seed {seed}");
        }

        [Test]
        public void Every_I4_lap_carries_ruins_and_stays_in_budget()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                int ruins = RuinCount(parts);
                Assert.GreaterOrEqual(ruins, MinRuinsALap, $"seed {seed}: ruins a lap");
                Assert.LessOrEqual(ruins, lap.Count - 1, $"seed {seed}: one to a leg, none on the closing leg");
                Assert.LessOrEqual(parts.Count, MaxPrismsALap, $"seed {seed}: ruin prisms a lap");
            }
        }

        // Measured over Seeds on 2026-11-24 (slingshot_ruins_harness --report) on the 150 u lane: 4 ruins a lap (5 on
        // one seed), one on every leg of 380 u or more bar the closing one, 631 to 870 prisms (424 to 546 on the 60 u
        // lane it had first: a ruin stands round its lane, so a wider lane is a bigger ruin, not a missing one).
        const int MinRuinsALap = 4;
        const int MaxPrismsALap = 900;

        [Test]
        public void The_lane_is_the_Stoats_own_and_wider_than_Spearlines()
        {
            Assert.AreEqual(SlingshotRuins.ClearRadius, SlingshotRuins.ForIntensity(4).ClearRadius);
            Assert.Greater(SlingshotRuins.ClearRadius, SpearlineRuins.LaneRadius);
        }

        [Test]
        public void A_ruin_stands_only_on_a_long_leg_and_never_on_the_closing_one()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                foreach (var part in parts)
                {
                    Assert.Less(part.Leg, lap.Count - 1, $"seed {seed}: the leg into ring 0 carries no ruin");
                    float leg = (lap[part.Leg + 1].Position - lap[part.Leg].Position).magnitude;
                    Assert.GreaterOrEqual(leg, SpearlineRuins.MinLegLength, $"seed {seed} leg {part.Leg}");
                }
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            float lane = SlingshotRuins.ForIntensity(4).ClearRadius;
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(lap, parts[i]), lane - 0.01f,
                        $"seed {seed}: a ruin prism reaches into a lane");
                }
            }
        }

        [Test]
        public void The_lane_is_wider_than_the_mouth()
        {
            // A Stoat that threads the I4 ring is never asked for a narrower pass between rings.
            Assert.GreaterOrEqual(SlingshotRuins.ForIntensity(4).ClearRadius, 2f * SlingshotCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            float margin = SpearlineRuins.RingMargin;
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out _);
                foreach (var part in parts)
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(lap, part.Position), margin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void Every_prism_is_thick_and_turned()
        {
            foreach (int seed in Seeds)
            {
                foreach (var part in Ruins(4, seed, out _, out _))
                {
                    Assert.GreaterOrEqual(part.Scale.x, 8f);
                    Assert.GreaterOrEqual(part.Scale.y, 8f);
                    Assert.AreEqual(1f, part.Forward.magnitude, 1e-3f);
                    Assert.AreEqual(1f, part.Up.magnitude, 1e-3f);
                }
            }
        }

        [Test]
        public void No_wheel_fits_the_Stoats_legs_so_nothing_crosses_the_lane()
        {
            // The wheel is the one kind that sweeps the line. It needs 270 u (its 150 u rim and the 120 u ring margin)
            // from both rings; the Stoat's legs are about 460 u, so the generator lays a pergola in its place. If the
            // course ever grows legs that fit one, this is the test to read again (the wheel is posed by the controller).
            foreach (int seed in Seeds)
            {
                Ruins(4, seed, out _, out var wheels);
                Assert.AreEqual(0, wheels.Count, $"seed {seed}");
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
