using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Decoy Run's garden ruins (DECOY_RUN.md §2a): only the hardest rung has them, every zip the coach's plan flies and
    /// every straight leg keeps its clear lane, the space round every ring stays clear, a ruin stands only on a leg
    /// long enough to hold one, none turns, and the lay is a pure function of the rings.
    /// Runs offline too: Tools/Build/decoy_run_harness/run.sh.
    /// </summary>
    public class DecoyRunRuinsTests
    {
        const int Seeds = 200;
        const float EndSlack = 8f;

        static List<SpearlineRuinPart> Ruins(int intensity, int seed, out List<RaceGate> rings, out DecoyRunSettings s)
        {
            s = DecoyRunCourse.ForIntensity(intensity);
            rings = DecoyRunCourse.Generate(seed, s);
            var parts = new List<SpearlineRuinPart>();
            DecoyRunRuins.Lay(rings, s, intensity, new List<RaceGate>(), parts);
            return parts;
        }

        /// <summary>How near a ruin prism comes to the segment, along the prism's whole length.</summary>
        static float Distance(in SpearlineRuinPart part, Vector3 a, Vector3 b)
        {
            float best = float.MaxValue;
            for (int k = 0; k <= 8; k++)
            {
                Vector3 p = part.Position + part.Forward * ((k / 8f - 0.5f) * part.Scale.z);
                best = Mathf.Min(best, DecoyRunCourse.DistanceToSegment(p, a, b));
            }
            return best;
        }

        /// <summary>Every zip of the coach's race, as the Unity AI plans it: take-off to decoy 0, decoy 0 to decoy 1.</summary>
        static List<(Vector3 from, Vector3 to)> Zips(List<RaceGate> rings, in DecoyRunSettings s)
        {
            var zips = new List<(Vector3, Vector3)>();
            Vector3 hull = DecoyRunCourse.StartPoses(1)[0].position;
            int next = 0, travels = s.StartTimeLevel >= 5 ? 2 : 1;
            while (next < rings.Count)
            {
                Vector3 board = DecoyRunCourse.FlatNormal(rings, next, s);
                int want = DecoyRunCourse.RingsACycleTakes(next, travels, s);
                int made = DecoyRunCourse.PlanCycle(hull, board, rings, next, rings.Count, travels, want, s, out var plan, out _);
                Assert.Greater(made, 0, $"no plan from ring {next}");
                Vector3 takeOff = 0.5f * (plan.Jack0To + plan.Jack1To);
                zips.Add((takeOff, plan.Jack0Via));
                hull = plan.Jack0Via;
                if (plan.SecondTravel == 1)
                {
                    zips.Add((plan.Jack0Via, plan.Jack1Via));
                    hull = plan.Jack1Via;
                }
                next += made;
            }
            return zips;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            int courses = 0, total = 0, most = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                    Assert.AreEqual(0, Ruins(i, seed, out _, out _).Count, $"I{i} seed {seed}");
                var legs = new HashSet<int>();
                foreach (var part in Ruins(4, seed, out _, out _)) legs.Add(DecoyRunRuins.CourseLeg(part));
                if (legs.Count > 0) courses++;
                total += legs.Count;
                most = Mathf.Max(most, legs.Count);
            }
            // Decoy Run's legs are short (260-480 u), so a course carries three or four ruins, not Spearline's seven.
            Assert.GreaterOrEqual(courses, Seeds * 9 / 10, "courses with a ruin at I4");
            Assert.GreaterOrEqual(total, Seeds * 2, "ruins over every course at I4");
            Assert.LessOrEqual(most, 7, "ruins on one course");
        }

        [Test]
        public void Every_zip_and_every_straight_leg_keeps_its_clear_lane()
        {
            // The generator measures a short prism (16 u or less) at its centre, so an end may reach half its length in.
            float lane = DecoyRunRuins.ForIntensity(4).ClearRadius - EndSlack;
            Assert.Greater(lane, 50f);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out var s);
                var zips = Zips(rings, s);
                foreach (var part in parts)
                {
                    foreach (var (from, to) in zips)
                        Assert.GreaterOrEqual(Distance(part, from, to), lane, $"seed {seed}: a ruin prism in a zip's lane");
                    for (int leg = 0; leg + 1 < rings.Count; leg++)
                    {
                        if (DecoyRunRuins.IsHairpin(leg, s)) continue;   // flown from the decoy: the zips above hold it
                        Assert.GreaterOrEqual(Distance(part, rings[leg].Position, rings[leg + 1].Position), lane,
                            $"seed {seed}: a ruin prism in leg {leg}'s lane");
                    }
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var parts = Ruins(4, seed, out var rings, out _);
                foreach (var part in parts)
                    foreach (var ring in rings)
                        Assert.GreaterOrEqual(Distance(part, ring.Position, ring.Position) - ring.Radius,
                            SpearlineRuins.RingMargin - EndSlack, $"seed {seed}: a ruin prism near a ring");
            }
        }

        [Test]
        public void A_ruin_stands_only_on_a_leg_long_enough_and_none_turns()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var s = DecoyRunCourse.ForIntensity(4);
                var rings = DecoyRunCourse.Generate(seed, s);
                var line = new List<RaceGate>();
                var parts = new List<SpearlineRuinPart>();
                DecoyRunRuins.Lay(rings, s, 4, line, parts);
                Assert.AreEqual(2 * (rings.Count - 1), line.Count, "two gates a leg");

                // nothing is left out as a turning ruin: Decoy Run's legs cannot hold a wheel
                var raw = new List<SpearlineRuinPart>();
                var wheels = new List<SpearlineRuinWheel>();
                SpearlineRuins.Lay(line, DecoyRunRuins.ForIntensity(4), raw, wheels);
                Assert.AreEqual(0, wheels.Count, $"seed {seed}: a wheel");
                Assert.AreEqual(raw.Count, parts.Count);

                foreach (var part in parts)
                {
                    Assert.AreEqual(0, part.Leg % 2, "a ruin on the join between two legs");
                    int leg = DecoyRunRuins.CourseLeg(part);
                    float length = (rings[leg + 1].Position - DecoyRunRuins.LegStart(rings, leg, s)).magnitude;
                    Assert.GreaterOrEqual(length, SpearlineRuins.MinLegLength, $"seed {seed} leg {leg}");
                }
            }
        }

        [Test]
        public void The_racing_line_is_straight_and_a_hairpin_leaves_from_the_decoy()
        {
            var s = DecoyRunCourse.ForIntensity(4);
            var rings = DecoyRunCourse.Generate(7, s);
            var line = new List<RaceGate>();
            DecoyRunRuins.RacingLine(rings, s, line);
            int hairpins = 0;
            for (int leg = 0; leg + 1 < rings.Count; leg++)
            {
                var a = line[2 * leg];
                var b = line[2 * leg + 1];
                // a quarter of the way: a curve between two gates that share an axis still passes the chord's middle
                Vector3 quarter = SpearlineRuins.LinePoint(a, b, 0.25f);
                Assert.Less((quarter - Vector3.Lerp(a.Position, b.Position, 0.25f)).magnitude, 0.01f, $"leg {leg} is not straight");
                Assert.Less((b.Position - rings[leg + 1].Position).magnitude, 0.001f);
                bool hairpin = DecoyRunRuins.IsHairpin(leg, s);
                Assert.AreEqual(hairpin ? DecoyRunCourse.Overshoot : 0f, (a.Position - rings[leg].Position).magnitude, 0.01f);
                if (hairpin) hairpins++;
            }
            Assert.AreEqual(4, hairpins, "one hairpin a group at I4");
            for (int i = 1; i <= 3; i++)
                for (int leg = 0; leg < 11; leg++)
                    Assert.IsFalse(DecoyRunRuins.IsHairpin(leg, DecoyRunCourse.ForIntensity(i)), $"I{i} has no hairpin");
        }

        [Test]
        public void The_ruins_are_the_same_for_the_same_rings()
        {
            var a = Ruins(4, 11, out _, out _);
            var b = Ruins(4, 11, out _, out _);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Scale, b[i].Scale);
                Assert.AreEqual(a[i].Leg, b[i].Leg);
            }
        }
    }
}
