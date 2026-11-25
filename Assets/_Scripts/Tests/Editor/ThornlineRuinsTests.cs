using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Thornline's ruins (THORNLINE.md §2a): only the hardest rung has them; the lane down every thorn-to-thorn line and
    /// every snipe line, the space round every ring and thorn, and the lines the pilots start on stay clear of every
    /// standing ruin; a ruin stands only on the second half of a leg that may carry one; each kind is drawn once; the
    /// lay is a pure function of the thorns. Measured here against the THORNS, not against the lane list the generator
    /// was given. Runs headless too: Tools/Build/thornline_course_harness/run.sh.
    /// </summary>
    public class ThornlineRuinsTests
    {
        const int Seeds = 60;

        sealed class Laid
        {
            public readonly List<Vector3> Thorns = new();
            public readonly List<RaceGate> Lanes = new();
            public readonly List<int> RuinLegs = new();
            public readonly List<SpearlineRuinPart> Parts = new();
            public readonly List<SpearlineRuinWheel> Wheels = new();
            public ThornlineSettings Course;

            public bool Turns(int part)
            {
                foreach (var wheel in Wheels)
                    if (part >= wheel.First && part < wheel.First + wheel.Count) return true;
                return false;
            }
        }

        static Laid Lay(int intensity, int seed)
        {
            var laid = new Laid { Course = ThornlineCourse.ForIntensity(intensity) };
            Assert.IsNotNull(ThornlineCourse.Generate(seed, laid.Course, laid.Thorns), $"I{intensity} seed {seed}: no chain");
            ThornlineRuins.Lay(laid.Thorns, intensity, laid.Lanes, laid.RuinLegs, laid.Parts, laid.Wheels);
            return laid;
        }

        /// <summary>The part's centre and, for a long bar, its two ends.</summary>
        static IEnumerable<Vector3> Points(SpearlineRuinPart part)
        {
            yield return part.Position;
            if (part.Scale.z <= 16f) yield break;
            Vector3 half = part.Forward * (0.5f * part.Scale.z);
            yield return part.Position + half;
            yield return part.Position - half;
        }

        /// <summary>How near <paramref name="p"/> is to any line a Shrike slings down: every leg, and every snipe.</summary>
        static float LineDistance(Laid laid, Vector3 p)
        {
            var t = laid.Thorns;
            int legs = t.Count - 1;
            float best = float.MaxValue;
            for (int k = 0; k < legs; k++)
            {
                best = Mathf.Min(best, ThornlineCourse.DistanceToSegment(p, t[k], t[k + 1]));
                if (k + 2 <= legs && ThornlineCourse.IsSkipBend(k + 1, legs, laid.Course.SkipBends))
                    best = Mathf.Min(best, ThornlineCourse.DistanceToSegment(p, t[k], t[k + 2]));
            }
            return best;
        }

        [Test]
        public void Only_the_hardest_rung_has_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var below = Lay(i, seed);
                    Assert.AreEqual(0, below.Parts.Count, $"I{i} seed {seed}");
                    Assert.AreEqual(0, below.RuinLegs.Count, $"I{i} seed {seed}");
                }
                Assert.Greater(Lay(4, seed).Parts.Count, 0, $"I4 seed {seed}");
            }
        }

        [Test]
        public void The_lane_down_every_sling_line_is_clear_of_every_standing_ruin()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    if (laid.Turns(i)) continue;
                    foreach (var p in Points(laid.Parts[i]))
                        Assert.GreaterOrEqual(LineDistance(laid, p), SpearlineRuins.LaneRadius - 0.01f,
                            $"seed {seed}: a ruin prism reaches into a sling line's lane");
                }
            }
        }

        [Test]
        public void No_ruin_stands_near_a_ring_or_a_thorn()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                float thornRadius = ThornlineCourse.HalfDiagonal(laid.Course.ThornSize);
                for (int i = 0; i < laid.Parts.Count; i++)
                {
                    foreach (var p in Points(laid.Parts[i]))
                    {
                        for (int k = 0; k < laid.Thorns.Count; k++)
                        {
                            Assert.GreaterOrEqual((p - laid.Thorns[k]).magnitude - thornRadius, SpearlineRuins.RingMargin - 0.01f,
                                $"seed {seed}: a ruin prism stands at thorn {k}");
                            if (k == 0) continue;
                            Vector3 ring = (laid.Thorns[k - 1] + laid.Thorns[k]) * 0.5f;
                            Assert.GreaterOrEqual((p - ring).magnitude - laid.Course.RingRadius, SpearlineRuins.RingMargin - 0.01f,
                                $"seed {seed}: a ruin prism stands at ring {k - 1}");
                        }
                    }
                }
            }
        }

        [Test]
        public void The_start_lines_are_clear_at_any_seat_count()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                foreach (var part in laid.Parts)
                    foreach (var p in Points(part))
                        Assert.GreaterOrEqual(ThornlineRuins.SeatDistance(laid.Thorns[0], p), SpearlineRuins.LaneRadius,
                            $"seed {seed}: a ruin prism stands on a start line");
            }
        }

        [Test]
        public void A_ruin_stands_only_on_the_second_half_of_a_leg_that_may_carry_one()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                int legs = laid.Thorns.Count - 1;
                Assert.LessOrEqual(laid.RuinLegs.Count, SpearlineRuins.KindCount, $"seed {seed}");
                var ruinOf = new Dictionary<int, int>();   // lane index -> thorn leg
                foreach (int lane in laid.RuinLegs)
                {
                    int leg = -1;
                    for (int k = 0; k < legs; k++)
                        if (((laid.Thorns[k] + laid.Thorns[k + 1]) * 0.5f - laid.Lanes[lane].Position).magnitude < 0.01f
                            && (laid.Thorns[k + 1] - laid.Lanes[lane + 1].Position).magnitude < 0.01f) leg = k;
                    Assert.GreaterOrEqual(leg, 1, $"seed {seed}: a ruin leg is not a ring-to-far-thorn half past the first leg");
                    Assert.IsTrue(ThornlineRuins.Eligible(laid.Thorns, laid.Course, leg), $"seed {seed} leg {leg}");
                    Assert.IsFalse(ThornlineCourse.IsSkipBend(leg, legs, laid.Course.SkipBends)
                                   || ThornlineCourse.IsSkipBend(leg + 1, legs, laid.Course.SkipBends), $"seed {seed}: a ruin on a snipe's leg");
                    ruinOf[lane] = leg;
                }
                foreach (var part in laid.Parts)
                    Assert.IsTrue(ruinOf.ContainsKey(part.Leg), $"seed {seed}: a ruin prism on a stretch that was to stay bare");
                // Every other stretch is too short for the generator to lay on (the closing one carries none by its rule).
                for (int lane = 0; lane < laid.Lanes.Count - 1; lane++)
                {
                    if (ruinOf.ContainsKey(lane)) continue;
                    Assert.Less((laid.Lanes[lane + 1].Position - laid.Lanes[lane].Position).magnitude, SpearlineRuins.MinLegLength,
                        $"seed {seed}: bare stretch {lane} is long enough to carry a ruin");
                }
            }
        }

        [Test]
        public void No_kind_is_drawn_twice_in_a_courses_first_six_ruins()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                var seen = new bool[SpearlineRuins.KindCount];
                for (int i = 0; i < laid.RuinLegs.Count && i < SpearlineRuins.KindCount - 1; i++)
                {
                    int kind = SpearlineRuins.KindOf(laid.RuinLegs[i], laid.Lanes[0].Position);
                    Assert.IsFalse(seen[kind], $"seed {seed}: kind {kind} drawn twice");
                    seen[kind] = true;
                }
                Assert.LessOrEqual(laid.Wheels.Count, 1, $"seed {seed}");
            }
        }

        [Test]
        public void A_wheel_is_drawn_only_on_a_half_long_enough_to_turn_one()
        {
            float gate = Mathf.Max(ThornlineCourse.ForIntensity(4).RingRadius, ThornlineCourse.HalfDiagonal(ThornlineCourse.ForIntensity(4).ThornSize));
            Assert.IsFalse(ThornlineRuins.WheelFits(400f, gate));
            Assert.IsTrue(ThornlineRuins.WheelFits(800f, gate));
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                foreach (int lane in laid.RuinLegs)
                {
                    if (SpearlineRuins.KindOf(lane, laid.Lanes[0].Position) != SpearlineRuins.Wheel) continue;
                    float half = (laid.Lanes[lane + 1].Position - laid.Lanes[lane].Position).magnitude;
                    Assert.IsTrue(ThornlineRuins.WheelFits(half, gate), $"seed {seed}: a wheel drawn on a {half:F0} u half");
                }
            }
        }

        [Test]
        public void A_wheel_turns_on_a_sling_line_with_its_hub_open()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                foreach (var wheel in laid.Wheels)
                {
                    Assert.Less(LineDistance(laid, wheel.Centre), 0.05f, $"seed {seed}: the wheel is off its line");
                    foreach (float seconds in new[] { 0f, 2.3f, 11f })
                        for (int i = wheel.First; i < wheel.First + wheel.Count; i++)
                        {
                            SpearlineRuins.Pose(laid.Parts[i], wheel, seconds, out var position, out var forward, out _);
                            Vector3 d = position - wheel.Centre;
                            float radial = (d - wheel.Axis * Vector3.Dot(d, wheel.Axis)).magnitude;
                            bool spoke = Mathf.Abs(Vector3.Dot(forward, d.normalized)) > 0.9f;
                            // The sling catches a block whose CENTRE is within AnchorReach of its ray: the hub stays wider.
                            Assert.GreaterOrEqual(spoke ? radial - 0.5f * laid.Parts[i].Scale.z : radial,
                                SpearlineRuins.HubRadius - 0.5f, $"seed {seed}: the hub is not open at {seconds} s");
                            Assert.Greater(radial, ThornlineCourse.AnchorReach + ThornlineCourse.LaneOffset,
                                $"seed {seed}: a wheel stone would catch a sling down the line");
                        }
                }
            }
        }

        [Test]
        public void The_lay_is_a_pure_function_of_the_thorns()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var first = Lay(4, seed);
                var second = Lay(4, seed);
                Assert.AreEqual(first.Parts.Count, second.Parts.Count);
                Assert.AreEqual(first.Wheels.Count, second.Wheels.Count);
                for (int i = 0; i < first.Parts.Count; i++)
                {
                    Assert.AreEqual(first.Parts[i].Position, second.Parts[i].Position);
                    Assert.AreEqual(first.Parts[i].Scale, second.Parts[i].Scale);
                    Assert.AreEqual(first.Parts[i].Leg, second.Parts[i].Leg);
                }
            }
        }

        [Test]
        public void A_course_carries_enough_ruin_to_read_and_stays_in_budget()
        {
            int ruins = 0, prisms = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var laid = Lay(4, seed);
                var standing = new HashSet<int>();
                foreach (var part in laid.Parts) standing.Add(part.Leg);
                Assert.GreaterOrEqual(standing.Count, MinRuins, $"seed {seed}: too few ruins");
                Assert.GreaterOrEqual(laid.Parts.Count, MinPrisms, $"seed {seed}: too little to read as ruins");
                Assert.LessOrEqual(laid.Parts.Count, MaxPrisms, $"seed {seed}: over the prism budget");
                ruins += standing.Count;
                prisms += laid.Parts.Count;
            }
            Assert.GreaterOrEqual(ruins / (float)Seeds, MeanRuins, "mean ruins a course");
            Assert.GreaterOrEqual(prisms / (float)Seeds, MeanPrisms, "mean ruin prisms a course");
        }

        // Measured over seeds 1-60 at I4 (run.sh --ruins, 2026-11-24): 4 ruins on the worst course and 5.88 on the mean
        // one, 319 to 841 prisms, 619 on the mean. Spearline's own budget is 1,000.
        const int MinRuins = 4, MinPrisms = 250, MaxPrisms = 1000;
        const float MeanRuins = 5.5f, MeanPrisms = 550f;

        [Test]
        public void Every_part_is_turned_and_thick_and_names_its_ordinal()
        {
            var laid = Lay(4, 1);
            foreach (var part in laid.Parts)
            {
                Assert.AreEqual(1f, part.Forward.magnitude, 1e-3f);
                Assert.AreEqual(1f, part.Up.magnitude, 1e-3f);
                Assert.GreaterOrEqual(part.Scale.x, 8f);
                Assert.GreaterOrEqual(part.Scale.y, 8f);
                int ordinal = ThornlineRuins.Ordinal(laid.RuinLegs, part.Leg);
                Assert.AreEqual(part.Leg, laid.RuinLegs[ordinal]);
            }
        }
    }
}
