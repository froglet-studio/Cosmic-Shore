using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Slip Line's ruins (SLIP_LINE.md §3a): only I4 has them; the straight line flown ring to ring (and the run-up to
    /// ring 0) keeps its clear lane, measured here against the REAL rings and not the lap the generator was handed; no
    /// ruin stands at a ring; no ruin prism stands on a rail; the seven ruins are spread down the whole course and are
    /// seven different kinds; the lay is a pure function of the rings. Runs offline too:
    /// Tools/Build/slip_line_harness/run.sh.
    /// </summary>
    public class SlipLineRuinsTests
    {
        const int Seeds = 60;

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> rings, out List<Rail> rails,
                                             out List<SpearlineRuinWheel> wheels, out List<RaceGate> lap)
        {
            var settings = SlipLineCourse.ForIntensity(intensity);
            rings = SlipLineCourse.Generate(seed, settings);
            Assert.IsNotNull(rings, $"I{intensity} seed {seed}: no course");
            rails = new List<Rail>();
            SlipLineCourse.RailsFor(rings, settings, rails);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            lap = new List<RaceGate>();
            SlipLineRuins.Lay(rings, rails, intensity, lap, parts, wheels);
            return parts;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        /// <summary>The middle and both ends of a part's length.</summary>
        static IEnumerable<Vector3> Reach(SpearlineRuinPart part)
        {
            yield return part.Position;
            if (part.Scale.z <= 16f) yield break;
            Vector3 half = part.Forward * (0.5f * part.Scale.z);
            yield return part.Position + half;
            yield return part.Position - half;
        }

        static int Count(List<SpearlineRuinPart> parts)
        {
            int ruins = 0, leg = -1;
            foreach (var part in parts)
                if (part.Leg != leg) { leg = part.Leg; ruins++; }
            return ruins;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _, out _, out _).Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed, out _, out _, out _, out _).Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_line_flown_ring_to_ring_keeps_its_clear_lane()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out _, out var wheels, out _);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    foreach (var p in Reach(parts[i]))
                        for (int k = 0; k < rings.Count; k++)
                            Assert.GreaterOrEqual(
                                SlipLineCourse.DistanceToSegment(p, SlipLineCourse.LegStart(rings, k), rings[k].Position),
                                SpearlineRuins.LaneRadius - 0.5f, $"seed {seed}: a ruin prism reaches into the leg into ring {k}");
                }
            }
        }

        [Test]
        public void The_lane_is_at_least_as_wide_as_the_mouth()
        {
            Assert.GreaterOrEqual(SpearlineRuins.LaneRadius, SlipLineCourse.ForIntensity(4).RingRadius);
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out _, out _, out _);
                foreach (var part in parts)
                    foreach (var p in Reach(part))
                        foreach (var ring in rings)
                            Assert.GreaterOrEqual((p - ring.Position).magnitude - ring.Radius, SpearlineRuins.RingMargin - 0.5f,
                                $"seed {seed}: a ruin prism stands at a ring");
            }
        }

        [Test]
        public void No_ruin_prism_stands_on_a_rail()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out var rails, out var wheels, out _);
                Assert.Greater(rails.Count, 0);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    foreach (var p in Reach(parts[i]))
                        foreach (var rail in rails)
                            Assert.GreaterOrEqual(SlipLineCourse.DistanceToSegment(p, rail.Start, rail.End),
                                SlipLineRuins.RailClearance - 0.5f, $"seed {seed}: a ruin prism stands on the rail of ring {rail.Ring}");
                }
                foreach (var wheel in wheels)
                    Assert.IsFalse(SlipLineRuins.SweepsARail(rails, wheel), $"seed {seed}: a wheel sweeps a rail");
            }
        }

        [Test]
        public void The_rails_cost_some_ruin_and_the_filter_is_what_holds_them_clear()
        {
            // Negative control for the test above: the generator alone, on the same lap, does stand prisms on rails.
            int onRails = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                Ruins(4, seed, out _, out var rails, out _, out var lap);
                var raw = new List<SpearlineRuinPart>();
                SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(4), raw, new List<SpearlineRuinWheel>());
                foreach (var part in raw)
                    if (SlipLineRuins.RailDistance(rails, part) < SlipLineRuins.RailClearance) onRails++;
            }
            Assert.Greater(onRails, 0, "no ruin prism ever met a rail: the rail test above proves nothing");
        }

        [Test]
        public void Seven_ruins_of_seven_kinds_stand_down_the_whole_course()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out _, out _, out var lap);
                Assert.AreEqual(SpearlineRuins.ForIntensity(4).PerLap, Count(parts), $"seed {seed}");

                var kinds = new HashSet<int>();
                int leg = -1;
                foreach (var part in parts)
                {
                    Assert.GreaterOrEqual(part.Leg, leg, "ruins are laid leg after leg");
                    if (part.Leg == leg) continue;
                    leg = part.Leg;
                    Assert.IsTrue(kinds.Add(SpearlineRuins.KindOf(leg, lap[0].Position)), $"seed {seed}: a kind stands twice");
                    Assert.GreaterOrEqual((lap[leg + 1].Position - lap[leg].Position).magnitude, SpearlineRuins.MinLegLength);
                }

                // the first ruin stands in the first third of the course and the last in the last third
                Assert.Less(RingOfLeg(rings, lap, parts[0].Leg), rings.Count / 3, $"seed {seed}: the first ruin stands late");
                Assert.GreaterOrEqual(RingOfLeg(rings, lap, parts[parts.Count - 1].Leg), rings.Count * 2 / 3 - 1,
                    $"seed {seed}: the last ruin stands early");
            }
        }

        /// <summary>The real ring a lap leg leaves from.</summary>
        static int RingOfLeg(List<RaceGate> rings, List<RaceGate> lap, int leg)
        {
            for (int i = 0; i < rings.Count; i++)
                if (rings[i].Position == lap[leg].Position) return i;
            Assert.IsTrue(false, "a ruin's leg leaves from no ring");
            return -1;
        }

        [Test]
        public void The_lap_handed_to_the_generator_is_the_straight_race_line()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                Ruins(4, seed, out var rings, out _, out _, out var lap);
                for (int leg = 0; leg + 1 < lap.Count; leg++)
                {
                    Vector3 a = lap[leg].Position, b = lap[leg + 1].Position;
                    if ((b - a).sqrMagnitude < 1f) continue;
                    foreach (float t in new[] { 0.25f, 0.5f, 0.75f })
                        Assert.Less((SpearlineRuins.LinePoint(lap[leg], lap[leg + 1], t) - Vector3.Lerp(a, b, t)).magnitude, 0.05f,
                            $"seed {seed} leg {leg}: the lane bows off the straight line");
                }
                // every real leg is in the lap, whole or in two halves
                for (int k = 1; k < rings.Count; k++)
                {
                    Vector3 mid = 0.5f * (rings[k - 1].Position + rings[k].Position);
                    Assert.Less(SpearlineRuins.LaneDistance(lap, mid), 0.05f, $"seed {seed}: the leg into ring {k} is not a lane");
                }
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_rings()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var first = Ruins(4, seed, out _, out _, out var firstWheels, out _);
                var second = Ruins(4, seed, out _, out _, out var secondWheels, out _);
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

        [Test]
        public void A_course_carries_enough_ruin_to_read_stays_in_budget_and_every_prism_is_thick()
        {
            int total = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out _, out _, out _);
                total += parts.Count;
                Assert.GreaterOrEqual(parts.Count, MinPrisms, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(parts.Count, 1000, $"seed {seed}: over the prism budget (Spearline's)");
                foreach (var part in parts)
                {
                    Assert.GreaterOrEqual(part.Scale.x, 8f);
                    Assert.GreaterOrEqual(part.Scale.y, 8f);
                    Assert.GreaterOrEqual(part.Scale.z, 5f);
                }
            }
            Assert.GreaterOrEqual(total / (float)Seeds, MinMeanPrisms);
        }

        // Measured 2026-11-24 over these seeds: 551 to 765 prisms a course, mean 660 (Spearline: 650 to 900 a lap).
        const int MinPrisms = 450;
        const float MinMeanPrisms = 600f;

        [Test]
        public void A_wheel_that_would_sweep_a_rail_is_dropped_whole_and_the_rest_keep_their_order()
        {
            var rails = new List<Rail> { new(new Vector3(0f, 80f, -100f), new Vector3(0f, 80f, 100f), 1, 1, false) };
            var size = new Vector3(8f, 8f, 8f);
            var parts = new List<SpearlineRuinPart>
            {
                new(new Vector3(300f, 0f, 0f), Vector3.forward, Vector3.up, size, 1),     // stands clear
                new(new Vector3(0f, 100f, 0f), Vector3.forward, Vector3.up, size, 1),     // on the rail
                new(new Vector3(0f, 120f, 0f), Vector3.forward, Vector3.up, size, 3),     // wheel A, through the rail
                new(new Vector3(0f, -120f, 0f), Vector3.forward, Vector3.up, size, 3),
                new(new Vector3(900f, 120f, 0f), Vector3.forward, Vector3.up, size, 5),   // wheel B, far from it
                new(new Vector3(900f, -120f, 0f), Vector3.forward, Vector3.up, size, 5),
            };
            var wheels = new List<SpearlineRuinWheel>
            {
                new(Vector3.zero, Vector3.forward, 25f, 2, 2),
                new(new Vector3(900f, 0f, 0f), Vector3.forward, 25f, 4, 2),
            };
            SlipLineRuins.KeepOffRails(rails, parts, wheels);
            Assert.AreEqual(3, parts.Count);
            Assert.AreEqual(1, parts[0].Leg);
            Assert.AreEqual(5, parts[1].Leg);
            Assert.AreEqual(1, wheels.Count);
            Assert.AreEqual(1, wheels[0].First);
            Assert.AreEqual(2, wheels[0].Count);
        }
    }
}
