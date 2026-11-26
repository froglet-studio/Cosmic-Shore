using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Breach's garden ruins (BREACH.md §3a): only I4 has them, the lane down every leg and the space round every ring
    /// stay clear of every standing ruin, a leg into a walled ring and a hard-turn leg carry none, no ruin stands at a
    /// wall, a wheel is laid whole or not at all, there are enough ruins to read as ruins, and the lay is a pure
    /// function of the rings. Runs offline too: Tools/Build/breach_harness/run.sh.
    /// </summary>
    public class BreachRuinsTests
    {
        const int Seeds = 100;

        static List<RaceGate> Course(int intensity, int seed) => BreachCourse.Generate(seed, BreachCourse.ForIntensity(intensity));

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> rings,
                                             out List<SpearlineRuinWheel> wheels, out List<int> ruin)
        {
            rings = Course(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            ruin = new List<int>();
            BreachRuins.Lay(rings, intensity, parts, wheels, ruin);
            return parts;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        static int Standing(List<int> ruin)
        {
            int n = 0;
            foreach (int r in ruin)
                if (r >= 0) n++;
            return n;
        }

        static int RuinCount(List<int> ruin)
        {
            int top = -1;
            foreach (int r in ruin) top = Mathf.Max(top, r);
            return top + 1;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var parts = Ruins(i, seed, out _, out var wheels, out var ruin);
                    Assert.AreEqual(0, parts.Count + wheels.Count + ruin.Count, $"I{i} seed {seed}");
                }
                Ruins(4, seed, out _, out _, out var four);
                Assert.Greater(Standing(four), 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void Every_part_is_numbered()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out _, out var ruin);
                Assert.AreEqual(parts.Count, ruin.Count, $"seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_leg_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out var wheels, out var ruin);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (ruin[i] < 0 || Turns(wheels, i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(rings, parts[i]), BreachRuins.ClearRadius - 1e-3f,
                        $"seed {seed} part {i} (leg {parts[i].Leg})");
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out var wheels, out var ruin);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (ruin[i] < 0 || Turns(wheels, i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(rings, parts[i]), BreachRuins.RingMargin - 1e-3f,
                        $"seed {seed} part {i} (leg {parts[i].Leg})");
                }
            }
        }

        [Test]
        public void A_leg_into_a_wall_and_a_hard_turn_leg_carry_no_ruin()
        {
            var course = BreachCourse.ForIntensity(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out _, out var ruin);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (ruin[i] < 0) continue;
                    int into = parts[i].Leg + 1;
                    Assert.IsFalse(BreachCourse.IsWalled(into, course), $"seed {seed} part {i}: on the leg into walled ring {into}");
                    Assert.IsFalse(BreachCourse.IsSharp(into, course), $"seed {seed} part {i}: on the hard leg into ring {into}");
                }
            }
        }

        [Test]
        public void No_ruin_stands_at_a_wall()
        {
            var course = BreachCourse.ForIntensity(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out _, out var ruin);
                var wall = new List<(Vector3 position, int ring)>();
                BreachCourse.WallsFor(rings, course, wall);
                Assert.Greater(wall.Count, 0, $"seed {seed}: I4 has walls");
                for (int i = 0; i < parts.Count; i++)
                {
                    if (ruin[i] < 0) continue;
                    Assert.IsFalse(BreachRuins.ReachesWall(parts[i], rings, course, 0f), $"seed {seed} part {i}");
                    // And against the wall's own prisms, not only its ball.
                    float half = 0.5f * parts[i].Scale.magnitude;
                    foreach (var (position, _) in wall)
                        Assert.Greater((parts[i].Position - position).magnitude, half + BreachCourse.WallPrismSize,
                            $"seed {seed} part {i}: in a wall prism");
                }
            }
        }

        [Test]
        public void A_wheel_is_laid_whole_or_not_at_all()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out var wheels, out var ruin);
                foreach (var wheel in wheels)
                {
                    int standing = 0;
                    for (int i = wheel.First; i < wheel.First + wheel.Count && i < parts.Count; i++)
                        if (ruin[i] >= 0) standing++;
                    Assert.IsTrue(standing == 0 || standing == wheel.Count, $"seed {seed}: a wheel with {standing} of {wheel.Count}");
                }
            }
        }

        [Test]
        public void There_are_enough_ruins_to_read_as_ruins()
        {
            int fewest = int.MaxValue, most = 0;
            long prisms = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                Ruins(4, seed, out _, out _, out var ruin);
                int count = RuinCount(ruin);
                fewest = Mathf.Min(fewest, count);
                most = Mathf.Max(most, count);
                prisms += Standing(ruin);
            }
            // Six ordinary legs run into an unwalled ring at I4 (rings 1, 2, 5, 8, 11, 14).
            Assert.GreaterOrEqual(fewest, MinRuins, $"fewest ruins on a course: {fewest}");
            Assert.LessOrEqual(most, 6, $"most ruins on a course: {most}");
            Assert.GreaterOrEqual(prisms / Seeds, MinMeanPrisms, $"mean ruin prisms a course: {prisms / Seeds}");
            Assert.LessOrEqual(prisms / Seeds, MaxMeanPrisms, $"mean ruin prisms a course: {prisms / Seeds}");
        }

        // Measured 2026-11-24 over 100 seeds: 6 ruins on every course, 419 prisms a course, no wheel.
        const int MinRuins = 6;
        const int MinMeanPrisms = 300, MaxMeanPrisms = 600;

        [Test]
        public void The_ruins_are_the_same_for_the_same_rings()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var a = Ruins(4, seed, out _, out _, out var ruinA);
                var b = Ruins(4, seed, out _, out _, out var ruinB);
                Assert.AreEqual(a.Count, b.Count, $"seed {seed}");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].Position, b[i].Position, $"seed {seed} part {i}");
                    Assert.AreEqual(ruinA[i], ruinB[i], $"seed {seed} part {i}");
                }
            }
        }
    }
}
