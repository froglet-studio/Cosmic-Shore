using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Perch Run's garden ruins (PERCH_RUN.md §3a): only I4 has them; they are laid round the STRAIGHT lines the Goby
    /// darts; every standing ruin prism keeps the 60 u lane down every leg and the start run, and 120 u from every
    /// ring's mouth; ruins stand only on the long legs; every dart the AI plans from a ring passes them; no wheel (the
    /// one kind that sweeps the line) fits a leg here; the lay is a pure function of the rings. Measured here on the course's own
    /// segments, not through the generator's helpers. Runs offline too: Tools/Build/perch_run_harness/run.sh.
    /// </summary>
    public class PerchRunRuinsTests
    {
        const int Seeds = 400;
        // The generator tests a short prism (16 u or less) at its centre, so an end can reach half of that nearer.
        const float Slack = 8.5f;

        static List<RaceGate> Course(int intensity, int seed) => PerchRunCourse.Generate(seed, PerchRunCourse.ForIntensity(intensity));

        static List<SpearlineRuinPart> Ruins(List<RaceGate> rings, int intensity, out List<SpearlineRuinWheel> wheels)
        {
            var parts = new List<SpearlineRuinPart>();
            wheels = new List<SpearlineRuinWheel>();
            PerchRunRuins.Lay(rings, intensity, new List<RaceGate>(), parts, wheels);
            return parts;
        }

        static bool Turns(List<SpearlineRuinWheel> wheels, int part)
        {
            foreach (var wheel in wheels)
                if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
            return false;
        }

        /// <summary>Points along a part's length (its local z), end to end.</summary>
        static IEnumerable<Vector3> Along(SpearlineRuinPart part)
        {
            const int samples = 9;
            for (int k = 0; k < samples; k++)
                yield return part.Position + part.Forward * ((k / (float)(samples - 1) - 0.5f) * part.Scale.z);
        }

        /// <summary>How near a point comes to any line the Goby flies: every leg, and the run from the start line to ring 0.</summary>
        static float LaneDistance(List<RaceGate> rings, Vector3 p)
        {
            Vector3 start = rings[0].Position - rings[0].Axis * PerchRunCourse.StartStandoff;
            float best = PerchRunCourse.DistanceToSegment(p, start, rings[0].Position);
            for (int k = 1; k < rings.Count; k++)
                best = Mathf.Min(best, PerchRunCourse.DistanceToSegment(p, rings[k - 1].Position, rings[k].Position));
            return best;
        }

        static int RuinCount(List<SpearlineRuinPart> parts)
        {
            var legs = new HashSet<int>();
            foreach (var part in parts) legs.Add(part.Leg);
            return legs.Count;
        }

        [Test]
        public void Only_the_reef_has_ruins()
        {
            for (int i = 1; i <= 3; i++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.AreEqual(0, Ruins(Course(i, seed), i, out _).Count, $"I{i} seed {seed}: ruins below I4");
        }

        [Test]
        public void The_reef_has_three_to_six_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(Course(4, seed), 4, out _);
                int ruins = RuinCount(parts);
                // the three hairpins' legs out always carry one; the first leg and the two blocked legs when 380 u or more
                Assert.GreaterOrEqual(ruins, 3, $"seed {seed}: {ruins} ruins");
                Assert.LessOrEqual(ruins, 6, $"seed {seed}: {ruins} ruins");
                // measured over these seeds: 5.3 ruins and about 520 prisms a course (196 to 719)
                Assert.GreaterOrEqual(parts.Count, 60 * ruins / 2, $"seed {seed}: {parts.Count} prisms in {ruins} ruins is too thin to read");
            }
        }

        [Test]
        public void The_dart_lap_is_the_straight_legs()
        {
            var rings = Course(4, 7);
            var lap = new List<RaceGate>();
            PerchRunRuins.DartLap(rings, lap);
            Assert.AreEqual(2 * rings.Count, lap.Count);
            for (int k = 0; k + 1 < rings.Count; k++)
            {
                int leg = PerchRunRuins.LapLeg(k);
                Assert.AreEqual(k, PerchRunRuins.CourseLeg(leg));
                for (int j = 0; j <= 8; j++)
                {
                    float t = j / 8f;
                    Vector3 flown = SpearlineRuins.LinePoint(lap[leg], lap[leg + 1], t);
                    Vector3 straight = Vector3.Lerp(rings[k].Position, rings[k + 1].Position, t);
                    Assert.Less((flown - straight).magnitude, 0.05f, $"leg {k} at {t}: the lane is not the dart's line");
                }
                // the leg between them has no length: the same ring twice
                Assert.Less((lap[leg + 1].Position - lap[(leg + 2) % lap.Count].Position).magnitude, k + 2 < rings.Count ? 0.001f : float.MaxValue);
            }
        }

        [Test]
        public void Ruins_stand_only_on_the_long_legs()
        {
            var s = PerchRunCourse.ForIntensity(4);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                foreach (var part in Ruins(rings, 4, out _))
                {
                    Assert.IsTrue(part.Leg >= 2 && part.Leg % 2 == 0, $"seed {seed}: a ruin on lap leg {part.Leg}, which is no course leg");
                    int k = PerchRunRuins.CourseLeg(part.Leg);
                    float length = (rings[k + 1].Position - rings[k].Position).magnitude;
                    Assert.GreaterOrEqual(length, SpearlineRuins.MinLegLength, $"seed {seed}: a ruin on leg {k}, {length:F0} u long");
                    Assert.IsFalse(PerchRunCourse.StartsDouble(k, s) || PerchRunCourse.StartsDouble(k + 1, s),
                        $"seed {seed}: a ruin on leg {k}, a double's");
                    Assert.IsFalse(PerchRunCourse.IsHairpin(k + 1, s), $"seed {seed}: a ruin on leg {k}, a hairpin's short leg in");
                }
            }
        }

        [Test]
        public void The_dart_lane_stays_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                var parts = Ruins(rings, 4, out var wheels);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    foreach (var p in Along(parts[i]))
                        Assert.GreaterOrEqual(LaneDistance(rings, p), SpearlineRuins.LaneRadius - Slack,
                            $"seed {seed}: ruin prism {i} (leg {PerchRunRuins.CourseLeg(parts[i].Leg)}) stands in the dart lane");
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                var parts = Ruins(rings, 4, out var wheels);
                for (int i = 0; i < parts.Count; i++)
                {
                    if (Turns(wheels, i)) continue;
                    foreach (var p in Along(parts[i]))
                        foreach (var ring in rings)
                            Assert.GreaterOrEqual((p - ring.Position).magnitude - ring.Radius, SpearlineRuins.RingMargin - Slack,
                                $"seed {seed}: ruin prism {i} stands too near a ring");
                }
            }
        }

        [Test]
        public void No_wheel_fits_a_perch_run_leg()
        {
            // A wheel needs 270 u from its hub to every ring's mouth: a leg of about 650 u, and the longest here is 560.
            // So the kind that sweeps the line never stands on a dart's line; its turn goes to a pergola. The controller
            // poses no wheel: if this ever fails, PerchRunController.Ruins.cs has to turn them (as Spearline's does).
            for (int seed = 1; seed <= Seeds; seed++)
            {
                Ruins(Course(4, seed), 4, out var wheels);
                Assert.AreEqual(0, wheels.Count, $"seed {seed}: a wheel on a Perch Run leg");
            }
        }

        [Test]
        public void Every_dart_the_ai_plans_passes_the_standing_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = Course(4, seed);
                var coral = new List<Coral>();
                PerchRunCourse.CoralFor(rings, PerchRunCourse.ForIntensity(4), coral);
                var parts = Ruins(rings, 4, out var wheels);
                Vector3 from = PerchRunCourse.StartPoses(1)[0].position;
                for (int next = 0; next < rings.Count; next++)
                {
                    Vector3 aim = PerchRunCourse.PlanDart(from, rings, next, coral, out _);
                    for (int i = 0; i < parts.Count; i++)
                    {
                        if (Turns(wheels, i)) continue;
                        // a prism's cross-section reaches half its diagonal from its centre line
                        float reach = 0.5f * Mathf.Sqrt(parts[i].Scale.x * parts[i].Scale.x + parts[i].Scale.y * parts[i].Scale.y);
                        foreach (var p in Along(parts[i]))
                            Assert.Greater(PerchRunCourse.DistanceToSegment(p, from, aim), reach + PerchRunCourse.HullRadius,
                                $"seed {seed}: the dart at ring {next} stops on ruin prism {i}");
                    }
                    from = rings[next].Position;
                }
            }
        }

        [Test]
        public void Every_ruin_prism_is_thick_and_the_lay_is_the_same_twice()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var rings = Course(4, seed);
                var a = Ruins(rings, 4, out var wa);
                var b = Ruins(rings, 4, out var wb);
                Assert.AreEqual(a.Count, b.Count, $"seed {seed}");
                Assert.AreEqual(wa.Count, wb.Count, $"seed {seed}");
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.IsTrue(a[i].Position == b[i].Position && a[i].Scale == b[i].Scale && a[i].Leg == b[i].Leg, $"seed {seed}: prism {i} differs");
                    Assert.GreaterOrEqual(Mathf.Min(a[i].Scale.x, Mathf.Min(a[i].Scale.y, a[i].Scale.z)), 8f, $"seed {seed}: prism {i} is thin");
                }
            }
        }
    }
}
