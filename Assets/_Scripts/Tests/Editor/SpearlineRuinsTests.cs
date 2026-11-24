using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Spearline's garden ruins (SPEARLINE.md §2a): only the hardest rung has them, the lane down every leg and the
    /// space round every ring stay clear of every STANDING ruin, the one turning kind (the wheel) leaves its hub open and
    /// sweeps no other leg, the lay is a pure function of the lap, and there is enough of each kind to read as a ruin.
    /// The same file runs headless through <c>Tools/Build/spearline_harness</c>.
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
                    Vector3 half = parts[i].Forward * (0.5f * parts[i].Scale.z);
                    if (parts[i].Scale.z > 16f)
                    {
                        Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position + half), SpearlineRuins.LaneRadius - 0.01f);
                        Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(lap, parts[i].Position - half), SpearlineRuins.LaneRadius - 0.01f);
                    }
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

        [Test]
        public void A_lap_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            int total = 0;
            foreach (int seed in Seeds)
            {
                int count = Ruins(4, seed, out _, out _).Count;
                total += count;
                Assert.GreaterOrEqual(count, 450, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(count, 1000, $"seed {seed}: over the prism budget");
            }
            float mean = total / (float)Seeds.Length;
            Assert.GreaterOrEqual(mean, 650f, $"mean {mean}");
            Assert.LessOrEqual(mean, 900f, $"mean {mean}");
        }

        [Test]
        public void Every_part_is_turned_and_sized_and_thick()
        {
            foreach (var part in Ruins(4, 1, out _, out _))
            {
                Assert.AreEqual(1f, part.Forward.magnitude, 1e-3f);
                Assert.AreEqual(1f, part.Up.magnitude, 1e-3f);
                Assert.Less(Mathf.Abs(Vector3.Dot(part.Forward, part.Up)), 0.6f, "forward and up nearly parallel");
                // Thick: no ruin prism is thinner than 8 u across, or than 5 u through.
                Assert.GreaterOrEqual(part.Scale.x, 8f);
                Assert.GreaterOrEqual(part.Scale.y, 8f);
                Assert.GreaterOrEqual(part.Scale.z, 5f);
            }
        }

        [Test]
        public void The_kinds_take_turns_and_most_stand_on_every_lap()
        {
            foreach (int seed in Seeds)
            {
                var lap = Lap(4, seed);
                var seen = new bool[SpearlineRuins.KindCount];
                for (int leg = 0; leg < lap.Count - 1; leg++)
                {
                    int kind = SpearlineRuins.KindOf(leg, lap[0].Position);
                    seen[kind] = true;
                    if (leg > 0) Assert.AreNotEqual(SpearlineRuins.KindOf(leg - 1, lap[0].Position), kind, $"seed {seed} leg {leg}");
                }
                // Eight rings give seven legs and there are seven kinds: each is drawn once a lap.
                foreach (bool kind in seen) Assert.IsTrue(kind, $"seed {seed}");
            }
        }

        [Test]
        public void A_wheel_turns_over_some_course_and_never_more_than_one_a_lap()
        {
            int courses = 0;
            foreach (int seed in Seeds)
            {
                Ruins(4, seed, out _, out var wheels);
                Assert.LessOrEqual(wheels.Count, 1, $"seed {seed}");
                if (wheels.Count > 0) courses++;
            }
            Assert.Greater(courses, Seeds.Length / 2, "the wheel fits on too few courses");
        }

        [Test]
        public void A_wheel_leaves_its_hub_open_stays_in_its_rim_and_sweeps_no_other_leg()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                foreach (var wheel in wheels)
                {
                    Assert.Greater(wheel.Count, 12);
                    Assert.AreEqual(SpearlineRuins.WheelDegreesPerSecond, Mathf.Abs(wheel.DegreesPerSecond), 1e-4f);
                    int legs = 0;
                    for (int leg = 0; leg < lap.Count; leg++)
                        if (SpearlineRuins.LegDistance(lap, leg, wheel.Centre) < SpearlineRuins.WheelRadius + SpearlineRuins.LaneRadius) legs++;
                    Assert.AreEqual(1, legs, $"seed {seed}: the wheel sweeps another leg's lane");
                    foreach (float seconds in new[] { 0f, 1.7f, 9.3f, 40f })
                    {
                        for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        {
                            SpearlineRuins.Pose(parts[i], wheel, seconds, out var position, out var forward, out var up);
                            Vector3 d = position - wheel.Centre;
                            float along = Vector3.Dot(d, wheel.Axis);
                            float radial = (d - wheel.Axis * along).magnitude;
                            float halfLength = 0.5f * parts[i].Scale.z;
                            // A spoke stone lies along the radius; a rim stone across it.
                            bool spoke = Mathf.Abs(Vector3.Dot(forward, d.normalized)) > 0.9f;
                            Assert.GreaterOrEqual(spoke ? radial - halfLength : radial, SpearlineRuins.HubRadius - 0.5f,
                                $"seed {seed}: the hub is not open at {seconds} s");
                            Assert.LessOrEqual(radial, SpearlineRuins.WheelRadius + 0.5f);
                            Assert.AreEqual(0f, along, 0.01f, "the wheel left its plane");
                            Assert.AreEqual(1f, forward.magnitude, 1e-3f);
                            Assert.AreEqual(1f, up.magnitude, 1e-3f);
                        }
                    }
                }
            }
        }

        [Test]
        public void A_wheel_really_turns_and_comes_round()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out _, out var wheels);
                foreach (var wheel in wheels)
                {
                    var part = parts[wheel.First];
                    SpearlineRuins.Pose(part, wheel, 0f, out var at0, out _, out _);
                    SpearlineRuins.Pose(part, wheel, 3f, out var at3, out _, out _);
                    SpearlineRuins.Pose(part, wheel, 360f / SpearlineRuins.WheelDegreesPerSecond, out var round, out _, out _);
                    Assert.AreEqual(0f, (at0 - part.Position).magnitude, 0.01f);
                    Assert.Greater((at3 - at0).magnitude, 10f, "the wheel does not turn");
                    Assert.AreEqual(0f, (round - at0).magnitude, 0.05f, "a full turn does not come round");
                }
            }
        }
    }
}
