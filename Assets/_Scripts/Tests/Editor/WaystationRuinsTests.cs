using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Waystation's garden ruins (<see cref="WaystationRuins"/>, WAYSTATION.md "Garden ruins"): only the hardest rung
    /// has them; the lane down the line a pilot really flies (cruise to the press point, then the fall) and the space
    /// round every ring and press point stay clear of every standing ruin; no ruin touches a tower wall or a lantern;
    /// a course carries at most one of each kind; the lay is a pure function of the course. The generator itself is
    /// Spearline's (<c>SpearlineRuinsTests</c>). Runs headless through <c>Tools/Build/waystation_tower_harness</c>.
    /// </summary>
    [TestFixture]
    public class WaystationRuinsTests
    {
        const int Clusters = 5;
        static readonly int[] Seeds = { 1, 2, 3, 7, 11, 23, 42, 101, 102, 103 };

        static List<RaceGate> Course(int intensity, int seed)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.RingTarget = Clusters * s.RingsPerCluster;
            s.InnerRadius = 478f;
            s.OuterRadius = 1080f;
            s.FirstClusterDirection = Vector3.up;
            return WaystationTower.Build(seed, s, intensity, out _);
        }

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> course,
                                             out List<SpearlineRuinWheel> wheels)
        {
            course = Course(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            WaystationRuins.Lay(course, intensity, parts, wheels);
            return parts;
        }

        static List<RaceGate> Line(List<RaceGate> course, int intensity)
        {
            var line = new List<RaceGate>();
            WaystationRuins.RacingLine(course, intensity, line);
            return line;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        static int RuinCount(List<SpearlineRuinPart> parts)
        {
            int count = 0, last = -1;
            foreach (var part in parts)
            {
                if (part.Leg == last) continue;
                last = part.Leg;
                count++;
            }
            return count;
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
        public void A_course_carries_ruins_where_a_leg_has_room_and_never_more_than_one_of_each_kind()
        {
            int total = 0;
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out _, out _);
                int ruins = RuinCount(parts);
                total += ruins;
                Assert.GreaterOrEqual(ruins, MinRuins, $"seed {seed}: no ruin at all");
                Assert.LessOrEqual(ruins, WaystationRuins.PerCourse, $"seed {seed}");
                // Ruins are numbered along the chain, 0, 1, 2..., each a run of at least MinParts prisms.
                int run = 0;
                for (int i = 0; i < parts.Count; i++)
                {
                    run++;
                    bool ends = i + 1 == parts.Count || parts[i + 1].Leg != parts[i].Leg;
                    if (!ends) continue;
                    if (i + 1 < parts.Count) Assert.AreEqual(parts[i].Leg + 1, parts[i + 1].Leg, $"seed {seed}");
                    Assert.GreaterOrEqual(run, WaystationRuins.MinParts, $"seed {seed}: ruin {parts[i].Leg} is a stub");
                    run = 0;
                }
            }
            Assert.GreaterOrEqual(total, MeanRuins * Seeds.Length, "ruins a course, on average");
        }

        const int MinRuins = 1, MeanRuins = 2;

        [Test]
        public void The_lane_down_the_flown_line_is_clear_of_every_standing_ruin()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var course, out var wheels);
                var line = Line(course, 4);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    Vector3 half = parts[i].Forward * (0.5f * parts[i].Scale.z);
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(line, parts[i].Position), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism stands in the lane");
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(line, parts[i].Position + half), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into the lane");
                    Assert.GreaterOrEqual(SpearlineRuins.LaneDistance(line, parts[i].Position - half), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into the lane");
                }
            }
        }

        [Test]
        public void The_flown_line_of_a_tower_hop_is_the_cruise_to_the_press_point_and_then_the_fall()
        {
            var s = WaystationTowerSettings.ForIntensity(4);
            foreach (int seed in Seeds)
            {
                var course = Course(4, seed);
                var line = Line(course, 4);
                int towers = 0, at = 0;
                for (int i = 0; i < course.Count; i++)
                {
                    if (i >= 1 && WaystationTower.IsTowerRing(course[i], s))
                    {
                        towers++;
                        Vector3 press = WaystationTower.PressOf(course, i, s);
                        Assert.AreEqual(press, line[at].Position);
                        Assert.AreEqual(press, line[at + 1].Position);
                        // Straight in (along the heading the pilot arrives on) and straight down (along the ring's axis).
                        Vector3 mid = SpearlineRuins.LinePoint(line[at + 1], line[at + 2], 0.5f);
                        Assert.Less(Vector3.Distance(mid, 0.5f * (press + course[i].Position)), 0.5f, $"seed {seed} ring {i}: the fall bends");
                        Vector3 cruise = SpearlineRuins.LinePoint(line[at - 1], line[at], 0.5f);
                        Assert.Less(Vector3.Distance(cruise, 0.5f * (line[at - 1].Position + press)), 0.5f, $"seed {seed} ring {i}: the cruise bends");
                        at += 2;
                    }
                    Assert.AreEqual(course[i].Position, line[at].Position);
                    at++;
                }
                Assert.AreEqual(line.Count, at);
                Assert.Greater(towers, 0, $"seed {seed}: a course with no tower ring");
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring_or_a_press_point()
        {
            var s = WaystationTowerSettings.ForIntensity(4);
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var course, out _);
                foreach (var part in parts)
                {
                    Assert.GreaterOrEqual(SpearlineRuins.RingClearance(course, part.Position), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
                    for (int i = 1; i < course.Count; i++)
                    {
                        if (!WaystationTower.IsTowerRing(course[i], s)) continue;
                        Assert.GreaterOrEqual(Vector3.Distance(part.Position, WaystationTower.PressOf(course, i, s)),
                            SpearlineRuins.RingMargin - 0.01f, $"seed {seed}: a ruin prism stands at ring {i}'s press point");
                    }
                }
            }
        }

        [Test]
        public void No_ruin_touches_a_tower_wall_or_a_lantern()
        {
            var tiles = new List<WaystationTowerTile>();
            var lanterns = new List<WaystationLantern>();
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out var course, out var wheels);
                WaystationTower.Walls(course, 4, tiles);
                WaystationTower.Lanterns(course, 4, lanterns);
                Assert.Greater(tiles.Count, 0);
                for (int i = 0; i < parts.Count; i++)
                {
                    // The prism's own length as a segment, checked here without the generator's sampler.
                    Vector3 half = parts[i].Forward * (0.5f * parts[i].Scale.z);
                    Vector3 a = parts[i].Position - half, b = parts[i].Position + half;
                    float cross = 0.5f * Mathf.Max(parts[i].Scale.x, parts[i].Scale.y);
                    foreach (var tile in tiles)
                        Assert.GreaterOrEqual(WaystationTower.PointSegmentDistance(tile.Position, a, b) - cross,
                            WaystationRuins.WallClear, $"seed {seed}: a ruin prism stands in a tower wall");
                    foreach (var lantern in lanterns)
                        Assert.GreaterOrEqual(WaystationTower.PointSegmentDistance(lantern.Position, a, b) - cross,
                            WaystationRuins.WallClear, $"seed {seed}: a ruin prism stands in a lantern post");
                }
                // A wheel turns: nothing of a tower stands inside the ball it sweeps.
                foreach (var wheel in wheels)
                {
                    foreach (var tile in tiles)
                        Assert.Greater(Vector3.Distance(tile.Position, wheel.Centre), SpearlineRuins.WheelRadius + WaystationRuins.WallClear);
                    foreach (var lantern in lanterns)
                        Assert.Greater(Vector3.Distance(lantern.Position, wheel.Centre), SpearlineRuins.WheelRadius + WaystationRuins.WallClear);
                }
            }
        }

        [Test]
        public void A_wheel_names_its_own_prisms()
        {
            foreach (int seed in Seeds)
            {
                var parts = Ruins(4, seed, out _, out var wheels);
                foreach (var wheel in wheels)
                {
                    Assert.GreaterOrEqual(wheel.First, 0);
                    Assert.LessOrEqual(wheel.First + wheel.Count, parts.Count);
                    Assert.Greater(wheel.Count, 0);
                    for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                    {
                        Assert.AreEqual(parts[wheel.First].Leg, parts[i].Leg, $"seed {seed}: a wheel turns another ruin's prism");
                        // In the wheel's plane, between its hub and its rim.
                        Vector3 d = parts[i].Position - wheel.Centre;
                        Assert.Less(Mathf.Abs(Vector3.Dot(d, wheel.Axis)), 0.5f);
                        Assert.LessOrEqual(d.magnitude, SpearlineRuins.WheelRadius + 0.5f);
                        Assert.GreaterOrEqual(d.magnitude, SpearlineRuins.HubRadius);
                    }
                }
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_course()
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
                    Assert.AreEqual(first[i].Leg, second[i].Leg);
                }
            }
        }
    }
}
