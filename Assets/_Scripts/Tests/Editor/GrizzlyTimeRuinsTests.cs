using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Grizzly Time's I4 ruins (GRIZZLYTIME.md §0.2): Spearline's ruins on the Grizzly's own circuit, cut back for the
    /// way a Grizzly flies it. Only the hardest rung has them; the ring-to-ring lane, the space round every ring, the
    /// throw lane from every keg, the keg-to-keg lines and every keg's own space stay clear of every STANDING ruin; a
    /// wheel never sweeps a keg; the lay is a pure function of the lap; and enough stands to read as ruins.
    /// The same file runs headless through <c>Tools/Build/grizzly_time_course_harness</c>.
    /// </summary>
    public class GrizzlyTimeRuinsTests
    {
        const int Seeds = 60;

        static List<RaceGate> Circuit(int intensity, int seed)
        {
            var s = GrizzlyTimeCourse.ForIntensity(intensity);
            s.InnerRadius = 480f;
            s.OuterRadius = 1080f;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> lap,
                                             out List<SpearlineRuinWheel> wheels)
        {
            lap = Circuit(intensity, seed);
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            GrizzlyTimeRuins.Lay(lap, intensity, parts, wheels);
            return parts;
        }

        static List<GrizzlyTimeKegs.Keg> Kegs(List<RaceGate> lap)
        {
            var kegs = new List<GrizzlyTimeKegs.Keg>();
            GrizzlyTimeKegs.KegsFor(lap, kegs);
            return kegs;
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
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _).Count, $"I{i} seed {seed}");
                Assert.Greater(Ruins(4, seed, out _, out _).Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void Enough_stands_at_I4_to_read_as_ruins()
        {
            int prisms = 0, ruins = 0, most = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out _, out _);
                prisms += parts.Count;
                ruins += RuinCount(parts);
                most = Mathf.Max(most, parts.Count);
                Assert.LessOrEqual(RuinCount(parts), GrizzlyTimeCourse.GatesPerLap - 1, $"seed {seed}: one ruin a leg, none on the closing leg");
            }
            Assert.GreaterOrEqual(ruins / (float)Seeds, MinMeanRuins, "ruins a lap, mean over the sweep");
            Assert.GreaterOrEqual(prisms / (float)Seeds, MinMeanPrisms, "ruin prisms a lap, mean over the sweep");
            Assert.LessOrEqual(most, MaxPrisms, "ruin prisms on one lap");
        }

        /// <summary>The floor and the ceiling of the sweep (measured 2026-11-24, see GRIZZLYTIME.md §0.2).</summary>
        const float MinMeanRuins = 6.5f, MinMeanPrisms = 450f;
        const int MaxPrisms = 800;

        [Test]
        public void The_ring_to_ring_lane_and_every_ring_are_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    Assert.GreaterOrEqual(SpearlineRuins.PartLaneDistance(lap, parts[i]), SpearlineRuins.LaneRadius - 0.01f,
                        $"seed {seed}: a ruin prism reaches into the ring-to-ring lane");
                    Assert.GreaterOrEqual(SpearlineRuins.PartRingClearance(lap, parts[i]), SpearlineRuins.RingMargin - 0.01f,
                        $"seed {seed}: a ruin prism stands at a ring");
                }
            }
        }

        [Test]
        public void The_throw_lane_and_the_keg_lines_are_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                var kegs = Kegs(lap);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    // Measured here, not with the generator's own distance: the centre, and the ends of a long bar.
                    Vector3 half = parts[i].Scale.z > 16f ? parts[i].Forward * (0.5f * parts[i].Scale.z) : Vector3.zero;
                    for (int end = -1; end <= 1; end++)
                        Assert.GreaterOrEqual(KegLanes(lap, kegs, parts[i].Position + half * end), GrizzlyTimeRuins.LaneRadius - 0.01f,
                            $"seed {seed}: a ruin prism reaches into a throw lane or a keg line");
                }
            }
        }

        /// <summary>The nearest of: keg to next ring (the throw), keg to next keg, ring to next keg.</summary>
        static float KegLanes(List<RaceGate> lap, List<GrizzlyTimeKegs.Keg> kegs, Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < kegs.Count; k++)
            {
                int next = (k + 1) % kegs.Count;
                best = Mathf.Min(best, SegmentDistance(p, kegs[k].Centre, lap[next].Position));
                best = Mathf.Min(best, SegmentDistance(p, kegs[k].Centre, kegs[next].Centre));
                best = Mathf.Min(best, SegmentDistance(p, lap[k].Position, kegs[next].Centre));
            }
            return best;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float t = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0f;
            return (a + d * t - p).magnitude;
        }

        [Test]
        public void No_ruin_stands_or_sweeps_in_a_kegs_space()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                var kegs = Kegs(lap);
                // A turning part is anywhere on its circle, so a wheel is held by its centre below.
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    foreach (var keg in kegs)
                        Assert.GreaterOrEqual((parts[i].Position - keg.Centre).magnitude, GrizzlyTimeRuins.KegClear - 0.01f,
                            $"seed {seed}: a ruin prism stands in keg {keg.Ring}'s space");
                }
            }
            // A wheel near a keg is rare (2 of 192 wheels in 400 laps, 2026-11-24), so the wheels get the wide sweep.
            for (int seed = 1; seed <= WheelSeeds; seed++)
            {
                Ruins(4, seed, out var lap, out var wheels);
                foreach (var wheel in wheels)
                    foreach (var keg in Kegs(lap))
                        Assert.GreaterOrEqual((wheel.Centre - keg.Centre).magnitude, SpearlineRuins.WheelRadius + GrizzlyTimeRuins.KegClear - 0.01f,
                            $"seed {seed}: a wheel sweeps keg {keg.Ring}'s space");
            }
        }

        const int WheelSeeds = 400;

        [Test]
        public void A_wheel_is_whole_and_its_range_is_its_own_parts()
        {
            int seen = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var lap, out var wheels);
                var raw = new List<SpearlineRuinPart>();
                var rawWheels = new List<SpearlineRuinWheel>();
                SpearlineRuins.Lay(lap, SpearlineRuins.ForIntensity(4), raw, rawWheels);
                foreach (var wheel in wheels)
                {
                    seen++;
                    Assert.LessOrEqual(wheel.First + wheel.Count, parts.Count, $"seed {seed}: a wheel's range runs off the list");
                    var source = rawWheels.Find(w => w.Centre == wheel.Centre);
                    Assert.AreEqual(source.Count, wheel.Count, $"seed {seed}: a wheel lost parts");
                    for (int k = 0; k < wheel.Count; k++)
                    {
                        Assert.AreEqual(raw[source.First + k].Position, parts[wheel.First + k].Position);
                        Assert.LessOrEqual((parts[wheel.First + k].Position - wheel.Centre).magnitude, SpearlineRuins.WheelRadius + 20f);
                    }
                }
            }
            Assert.Greater(seen, 0, "the sweep holds at least one wheel");
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_lap()
        {
            for (int seed = 1; seed <= 10; seed++)
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

        [Test]
        public void Every_part_is_thick()
        {
            foreach (var part in Ruins(4, 1, out _, out _))
            {
                Assert.GreaterOrEqual(part.Scale.x, 8f);
                Assert.GreaterOrEqual(part.Scale.y, 8f);
            }
        }
    }
}
