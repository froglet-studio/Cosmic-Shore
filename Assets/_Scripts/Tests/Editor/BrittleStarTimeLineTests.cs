using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Brittle Star Time's Shift Line (BRITTLESTARTIME.md section 0): the let-go gates stand where a corner is tighter than
    /// the Speed the hull carries and nowhere else, never in a mouth and never on the line; the race coach is ordered by
    /// skill and deterministic per seat; the heat grades by carried speed; the laps scale per rung; and the hull AI
    /// policy's mode variant can never engage. Pure and deterministic: no scene, no clock, no randomness.
    /// </summary>
    public class BrittleStarTimeLineTests
    {
        const float Inner = 480f, Outer = 1080f;
        const int Seeds = 200;

        static List<RaceGate> Lap(int intensity, int seed)
        {
            if (BrittleStarTimeCourse.BuildLap(intensity, seed, Inner, Outer, BrittleStarTimeCourse.GatesPerLap, out var lap))
                return lap;
            var s = BrittleStarTimeCourse.Gearbox();
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            return HeadlongCircuit.Generate(seed, s);
        }

        static float Top(int intensity) =>
            intensity >= 4 ? BrittleStarTimeCourse.SpeedModeSpeedAtTime5 : BrittleStarTimeCourse.SpeedModeSpeed;

        static int[] LetGoRings(List<RaceGate> lap, int intensity) =>
            Enumerable.Range(0, lap.Count).Where(k => BrittleStarTimeLine.LetGoDistance(lap, k, Top(intensity)) > 0f).ToArray();

        [Test]
        public void Flat_out_rungs_stand_no_let_go_gate()
        {
            // I1 (the Gearbox) and I2 (the feather slalom): Speed Mode's circle holds every corner a pilot flies.
            for (int intensity = 1; intensity <= 2; intensity++)
                for (int seed = 1; seed <= Seeds; seed++)
                    Assert.AreEqual(0, LetGoRings(Lap(intensity, seed), intensity).Length,
                        $"I{intensity} seed {seed}: a let-go gate on a flat-out rung");
        }

        [Test]
        public void Hairpins_let_go_at_both_apexes_only()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = LetGoRings(Lap(3, seed), 3);
                CollectionAssertEqual(new[] { 3, 7 }, rings, $"I3 seed {seed}");
            }
        }

        [Test]
        public void Corkscrew_lets_go_on_the_return_and_holds_the_coil()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var rings = LetGoRings(Lap(4, seed), 4);
                CollectionAssertEqual(new[] { 0, 6, 7 }, rings, $"I4 seed {seed}");
            }
        }

        static void CollectionAssertEqual(int[] expected, int[] actual, string what) =>
            Assert.AreEqual(string.Join(",", expected), string.Join(",", actual), $"{what}: let-go rings");

        [Test]
        public void A_let_go_is_the_coast_down_to_the_corner_and_fits_its_leg()
        {
            for (int intensity = 3; intensity <= 4; intensity++)
                for (int seed = 1; seed <= 40; seed++)
                {
                    var lap = Lap(intensity, seed);
                    foreach (int k in LetGoRings(lap, intensity))
                    {
                        float top = Top(intensity), corner = BrittleStarTimeLine.CornerSpeed(lap, k, top);
                        float d = BrittleStarTimeLine.LetGoDistance(lap, k, top);
                        Assert.Less(corner, BrittleStarTimeLine.LetGoBelowShare * top);
                        Assert.AreEqual(BrittleStarTimeCourse.CoastDistance(top, corner) + BrittleStarTimeLine.LetGoBlendMargin, d, 1e-3f);
                        Assert.Greater(d, 60f, $"I{intensity} ring {k}: a let-go too short to read");
                        int n = lap.Count;
                        float leg = (lap[k].Position - lap[(k - 1 + n) % n].Position).magnitude;
                        Assert.LessOrEqual((BrittleStarTimeLine.LetGoPoint(lap, k, d) - lap[k].Position).magnitude, 0.9f * leg + 1e-3f);
                    }
                }
        }

        [Test]
        public void Let_go_posts_are_two_columns_clear_of_every_mouth_and_the_line()
        {
            for (int intensity = 3; intensity <= 4; intensity++)
                for (int seed = 1; seed <= 40; seed++)
                {
                    var lap = Lap(intensity, seed);
                    var posts = BrittleStarTimeLine.Marks(lap, Top(intensity));
                    Assert.AreEqual(LetGoRings(lap, intensity).Length * 2 * BrittleStarTimeLine.PostsPerColumn, posts.Count);
                    foreach (var p in posts)
                    {
                        // Clear of the line through the gate: at least the mouth's radius out from the gate's centre.
                        float d = BrittleStarTimeLine.LetGoDistance(lap, p.Ring, Top(intensity));
                        Vector3 centre = BrittleStarTimeLine.LetGoPoint(lap, p.Ring, d);
                        Vector3 across = Vector3.ProjectOnPlane(p.Position - centre, BrittleStarTimeLine.Approach(lap, p.Ring));
                        Assert.GreaterOrEqual(across.magnitude, lap[p.Ring].Radius, "a post stands in the line");
                        // Never inside a ring's mouth.
                        foreach (var g in lap)
                        {
                            float along = Vector3.Dot(p.Position - g.Position, g.Axis);
                            float off = Vector3.ProjectOnPlane(p.Position - g.Position, g.Axis).magnitude;
                            Assert.IsFalse(Mathf.Abs(along) < BrittleStarTimeLine.PostSize && off < g.Radius,
                                $"I{intensity} seed {seed}: a post in ring mouth");
                        }
                    }
                }
        }

        [Test]
        public void The_line_comes_down_the_facing_and_takes_an_apex_at_its_centre()
        {
            var hair = Lap(3, 1);
            Assert.IsFalse(BrittleStarTimeLine.FacesLeg(hair, 3), "the hairpin apex faces across its leg");
            Assert.IsTrue(BrittleStarTimeLine.FacesLeg(hair, 1), "a straight's ring faces down it");
            Vector3 far = hair[3].Position - BrittleStarTimeLine.Flow(hair, 3) * 500f + Vector3.up * 3f;
            Assert.AreEqual(0f, (BrittleStarTimeLine.SteerPoint(hair, 3, far) - hair[3].Position).magnitude, 1e-3f);
            Vector3 back = hair[1].Position - BrittleStarTimeLine.Flow(hair, 1) * 400f;
            Vector3 aim = BrittleStarTimeLine.SteerPoint(hair, 1, back);
            Assert.Less(Vector3.Angle(hair[1].Position - aim, BrittleStarTimeLine.Flow(hair, 1)), 1f, "on the facing");
        }

        // ── The coach ──

        [Test]
        public void Coach_seat_errors_are_fixed_and_bounded()
        {
            for (int seat = 0; seat < 6; seat++)
                for (int ring = 0; ring < 8; ring++)
                {
                    float a = BrittleStarTimeCoach.Hash11(seat, ring);
                    Assert.AreEqual(a, BrittleStarTimeCoach.Hash11(seat, ring), 0f, "the hash is not deterministic");
                    Assert.LessOrEqual(Mathf.Abs(a), 1f);
                }
            Assert.AreNotEqual(BrittleStarTimeCoach.Hash11(1, 3), BrittleStarTimeCoach.Hash11(2, 3), "two seats err alike");
        }

        [Test]
        public void Coach_tables_are_in_range()
        {
            for (int i = 1; i <= 4; i++)
                for (int k = 0; k < 4; k++)
                {
                    var skill = (BrittleStarTimeCoach.Skill)k;
                    float follow = BrittleStarTimeCoach.NoseFollowFor(i, skill), share = BrittleStarTimeCoach.CornerShareFor(i, skill);
                    Assert.IsTrue(follow >= 0.8f && follow <= 16f, $"I{i} {skill}: nose follow {follow} outside 0.8-16/s");
                    Assert.IsTrue(share > 0f && share <= 1f, $"I{i} {skill}: corner share {share}");
                }
            Assert.AreEqual(BrittleStarTimeCoach.Skill.Easy, BrittleStarTimeCoach.SkillFor(1));
            Assert.AreEqual(BrittleStarTimeCoach.Skill.Legendary, BrittleStarTimeCoach.SkillFor(4));
        }

        [Test]
        public void Coach_holds_Speed_on_the_line_and_lets_go_inside_the_gate()
        {
            var lap = Lap(3, 1);
            float top = Top(3);
            const int seat = 1;
            var skill = BrittleStarTimeCoach.Skill.Medium;
            // Flat-out ring 1, nose on the line: Speed.
            Vector3 flow1 = BrittleStarTimeLine.Flow(lap, 1);
            Vector3 p = lap[1].Position - flow1 * 250f;
            Vector3 aim = BrittleStarTimeLine.SteerPoint(lap, 1, p);
            Assert.IsTrue(BrittleStarTimeCoach.WantsSpeed(lap, 1, p, (aim - p).normalized, top, seat, skill, 3, true));
            // Nose far off the line: no Speed.
            Assert.IsFalse(BrittleStarTimeCoach.WantsSpeed(lap, 1, p, -(aim - p).normalized, top, seat, skill, 3, true));
            // Hairpin apex 3, inside its let-go, nose on: coast (no Speed); well outside it: Speed.
            float letGo = BrittleStarTimeCoach.LetGoFor(lap, 3, top, seat, skill, 3);
            Assert.Greater(letGo, 0f);
            Vector3 along = BrittleStarTimeLine.Approach(lap, 3);
            Vector3 inside = lap[3].Position - along * (0.5f * letGo);
            Assert.IsFalse(BrittleStarTimeCoach.WantsSpeed(lap, 3, inside, (lap[3].Position - inside).normalized, top, seat, skill, 3, true));
        }

        [Test]
        public void A_cautious_skill_lets_go_no_later_than_a_bold_one()
        {
            // Over seats, Easy lets go at least as early as Legendary on the hairpins (its corner share is lower).
            var lap = Lap(3, 1);
            float easy = 0f, legend = 0f;
            for (int seat = 0; seat < 8; seat++)
            {
                easy += BrittleStarTimeCoach.LetGoFor(lap, 3, Top(3), seat, BrittleStarTimeCoach.Skill.Easy, 3);
                legend += BrittleStarTimeCoach.LetGoFor(lap, 3, Top(3), seat, BrittleStarTimeCoach.Skill.Legendary, 3);
            }
            Assert.Greater(easy, legend, "Easy lets go later than Legendary");
        }

        // ── The heat ──

        [Test]
        public void Heat_grades_by_the_speed_carried()
        {
            var hair = Lap(3, 1);
            float top = Top(3);
            var g1 = hair[1];
            Assert.AreEqual(BrittleStarThreadGrade.Missed, BrittleStarTimeHeat.Grade(hair, 1, g1.Radius + 1f, top, top));
            Assert.AreEqual(BrittleStarThreadGrade.FlatOut, BrittleStarTimeHeat.Grade(hair, 1, 0f, top, top));
            Assert.AreEqual(BrittleStarThreadGrade.Threaded, BrittleStarTimeHeat.Grade(hair, 1, 0.9f * g1.Radius, top, top),
                "flat out but off the heart");
            Assert.AreEqual(BrittleStarThreadGrade.Threaded, BrittleStarTimeHeat.Grade(hair, 1, 0f, 0.5f * top, top), "crawled");
            float corner = BrittleStarTimeLine.CornerSpeed(hair, 3, top);
            Assert.AreEqual(BrittleStarThreadGrade.CleanShift, BrittleStarTimeHeat.Grade(hair, 3, 0.9f * hair[3].Radius, corner, top),
                "a let-go ring asks only for the speed");
            Assert.AreEqual(BrittleStarThreadGrade.Threaded, BrittleStarTimeHeat.Grade(hair, 3, 0f, 0.5f * corner, top));
        }

        [Test]
        public void Heat_streak_laps_and_crossings()
        {
            Assert.AreEqual(3, BrittleStarTimeHeat.StreakAfter(2, BrittleStarThreadGrade.FlatOut));
            Assert.AreEqual(3, BrittleStarTimeHeat.StreakAfter(2, BrittleStarThreadGrade.CleanShift));
            Assert.AreEqual(2, BrittleStarTimeHeat.StreakAfter(2, BrittleStarThreadGrade.Threaded), "a slow thread holds");
            Assert.AreEqual(0, BrittleStarTimeHeat.StreakAfter(5, BrittleStarThreadGrade.Missed), "a miss ends it");
            Assert.IsTrue(BrittleStarTimeHeat.IsCalled(3) && BrittleStarTimeHeat.IsCalled(8) && BrittleStarTimeHeat.IsCalled(16)
                          && BrittleStarTimeHeat.IsCalled(32));
            Assert.IsFalse(BrittleStarTimeHeat.IsCalled(4) || BrittleStarTimeHeat.IsCalled(0) || BrittleStarTimeHeat.IsCalled(20));
            Assert.AreEqual(2, BrittleStarTimeHeat.LapEndedBy(16, 8));
            Assert.AreEqual(0, BrittleStarTimeHeat.LapEndedBy(15, 8));
            Assert.AreEqual(3f, BrittleStarTimeHeat.CrossingOffset(new Vector3(3, 0, -1), new Vector3(3, 0, 1), Vector3.zero, Vector3.forward), 1e-4f);
            Assert.IsTrue(float.IsPositiveInfinity(BrittleStarTimeHeat.CrossingOffset(new Vector3(0, 0, 1), new Vector3(0, 0, 2), Vector3.zero, Vector3.forward)));
            Assert.IsTrue(BrittleStarTimeHeat.IsAnAttempt(40f, 44f) && BrittleStarTimeHeat.IsAnAttempt(100f, 44f));
            Assert.IsFalse(BrittleStarTimeHeat.IsAnAttempt(400f, 44f), "a crossing far out on the plane is no attempt");
            Assert.IsFalse(BrittleStarTimeHeat.IsAnAttempt(float.PositiveInfinity, 44f));
            Assert.IsTrue(BrittleStarTimeHeat.SpeedStillDark(BrittleStarTimeHeat.SpeedDarkSeconds, 0, false));
            Assert.IsFalse(BrittleStarTimeHeat.SpeedStillDark(BrittleStarTimeHeat.SpeedDarkSeconds, 0, true));
            Assert.IsFalse(BrittleStarTimeHeat.SpeedStillDark(BrittleStarTimeHeat.SpeedDarkSeconds, 1, false));
        }

        // ── The laps and the AI policy variant ──

        [Test]
        public void Laps_scale_per_rung_and_keep_eight_rings_a_lap()
        {
            int[] laps = { 2, 3, 6, 6 };
            for (int i = 1; i <= 4; i++)
            {
                Assert.AreEqual(laps[i - 1], BrittleStarTimeCourse.LapsFor(i));
                Assert.AreEqual(laps[i - 1], BrittleStarTimeCourse.ScaleForRung(2, i), "the scene's 2 laps at I1");
                int target = BrittleStarTimeCourse.ScaleForRung(16, i);
                Assert.AreEqual(8 * laps[i - 1], target, "the authored 16 rings at I1");
                Assert.AreEqual(BrittleStarTimeCourse.GatesPerLap, Mathf.CeilToInt(target / (float)laps[i - 1]));
            }
        }

        const string Variant = "Assets/Resources/AIBoostPolicies/BrittleStarAIBoostPolicy_BrittleStarTime.asset";

        [Test]
        public void The_AI_policy_variant_never_engages_so_the_coach_is_the_one_writer()
        {
            Assert.IsTrue(File.Exists(Variant), $"{Variant} is missing");
            string text = File.ReadAllText(Variant);
            StringAssertContains(text, "m_Name: BrittleStarAIBoostPolicy_BrittleStarTime");
            StringAssertContains(text, "\n  alignDegrees: 0\n");
            StringAssertContains(text, "\n  engageRunwaySeconds: 999\n");
            for (int i = 1; i <= 4; i++)
                Assert.IsFalse(File.Exists(Variant.Replace(".asset", $"_I{i}.asset")),
                    $"an _I{i} variant would be picked first and bring back the hull's straight law");
        }

        static void StringAssertContains(string text, string what) =>
            Assert.IsTrue(text.Contains(what), $"'{what.Trim()}' not found");

        [Test]
        public void Speed_Mode_minimum_the_runtime_reads_is_the_course_constant()
        {
            // The runtime evaluates speedModeMultiplier through ElementalFloat (Min at Time 0); the older course test reads
            // its Value. Both are 6 today; this holds the one the game uses.
            string config = File.ReadAllText("Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset");
            var m = Regex.Match(config, @"speedModeMultiplier:\s*\n(?:\s+\w+: [^\n]*\n)*?\s+Min: ([0-9.]+)");
            Assert.IsTrue(m.Success, "speedModeMultiplier Min not found");
            Assert.AreEqual(BrittleStarTimeCourse.SpeedModeMultiplier,
                float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-4f);
        }
    }
}
