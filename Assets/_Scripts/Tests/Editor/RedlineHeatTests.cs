using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Redline as it is FLOWN since 2026-11-11 (REDLINE.md section 0): Soar is the Manta's Time level-5 upgrade, the card
    /// starts every Manta at Time 6 so it is lit from the gun, Time raises cruise and Soar both, and the local pilot's
    /// threads are graded and called (<see cref="RedlineHeat"/>). Pure, so <c>Tools/Build/redline_course_harness</c> runs
    /// these out of the editor beside the course suites.
    /// </summary>
    public class RedlineHeatTests
    {
        // ── The flown curve, from Manta.prefab and Manta.asset ───────────────

        [Test]
        public void The_flown_curve_matches_the_shipped_Manta()
        {
            // ThrottleScalerMultiplier 1 -> 1.5 and BoostSpeedMultiplier 1 -> 1.3 over Time 0-10 (Manta.prefab).
            Assert.AreEqual(180f, RedlineCourse.CruiseAt(0), 1e-3f);
            Assert.AreEqual(234f, RedlineCourse.CruiseAt(6), 1e-3f);
            Assert.AreEqual(270f, RedlineCourse.CruiseAt(10), 1e-3f);
            Assert.AreEqual(270f, RedlineCourse.CruiseAt(14), 1e-3f, "the earned band tops out at 10 here");
            // Full Soar: cruise x 4 x (1 + 0.03 L).
            Assert.AreEqual(1104.48f, RedlineCourse.SoarTop(6), 0.05f);
            Assert.AreEqual(1404f, RedlineCourse.SoarTop(10), 0.05f);
            // The circles: 252 u at Time 6, 259 at 10; the stick alone 175 at 6.
            Assert.AreEqual(252.2f, RedlineCourse.FullSoarRadius(6), 0.5f);
            Assert.AreEqual(258.9f, RedlineCourse.FullSoarRadius(10), 0.5f);
            Assert.AreEqual(174.6f, RedlineCourse.StickAloneRadius(6), 0.5f);
        }

        [Test]
        public void Soar_is_dark_below_its_upgrade()
        {
            Assert.IsFalse(RedlineCourse.SoarLit(4));
            Assert.IsTrue(RedlineCourse.SoarLit(5));
            Assert.AreEqual(RedlineCourse.CruiseAt(4), RedlineCourse.SoarSpeed(1f, 4), 1e-3f, "dark: both triggers buy nothing");
            Assert.AreEqual(RedlineCourse.CruiseAt(6), RedlineCourse.SoarSpeed(0.005f, 6), 1e-3f, "under the executor's 0.01 overlap");
            Assert.Greater(RedlineCourse.SoarSpeed(0.5f, 6), RedlineCourse.CruiseAt(6));
            Assert.Greater(RedlineCourse.SoarTop(5), 4f * RedlineCourse.CruiseAt(5));
        }

        [Test]
        public void Every_intensity_starts_with_Soar_lit_and_headroom()
        {
            for (int i = 1; i <= 4; i++)
            {
                int level = RedlineCourse.StartingTimeLevel(i);
                Assert.IsTrue(RedlineCourse.SoarLit(level), $"I{i}: Soar must be lit from the gun (Time {level})");
                Assert.GreaterOrEqual(level - RedlineCourse.MantaSoarRelockBelowLevel, 2,
                    $"I{i}: two petals of headroom before Soar relocks below {RedlineCourse.MantaSoarRelockBelowLevel}");
                Assert.LessOrEqual(level, RedlineCourse.MantaMaxTimeLevel);
            }
            Assert.AreEqual(RedlineCourse.StartingTimeLevel(1), RedlineCourse.StartingTimeLevel(0), "clamped low");
            Assert.AreEqual(RedlineCourse.StartingTimeLevel(4), RedlineCourse.StartingTimeLevel(9), "clamped high");
        }

        // ── The heat ─────────────────────────────────────────────────────────

        [Test]
        public void Grade_boundaries()
        {
            float mouth = 88f, top = RedlineCourse.SoarTop(6);
            Assert.AreEqual(RedlineThreadGrade.Missed, RedlineHeat.Grade(88.1f, mouth, top, 6));
            Assert.AreEqual(RedlineThreadGrade.Threaded, RedlineHeat.Grade(88f, mouth, top, 6), "the rim is still inside");
            Assert.AreEqual(RedlineThreadGrade.Threaded, RedlineHeat.Grade(44.1f, mouth, top, 6));
            Assert.AreEqual(RedlineThreadGrade.Redhot, RedlineHeat.Grade(44f, mouth, top, 6), "the heart's edge, flat out");
            Assert.AreEqual(RedlineThreadGrade.Redhot, RedlineHeat.Grade(0f, mouth, 0.8f * top, 6));
            Assert.AreEqual(RedlineThreadGrade.Clean, RedlineHeat.Grade(0f, mouth, 0.79f * top, 6), "eased: clean, not redhot");
            Assert.AreEqual(RedlineThreadGrade.Clean, RedlineHeat.Grade(0f, mouth, 2000f, 4), "Soar dark: never redhot");
        }

        [Test]
        public void The_streak_builds_on_clean_and_breaks_on_anything_else()
        {
            int s = 0;
            s = RedlineHeat.StreakAfter(s, RedlineThreadGrade.Clean);
            s = RedlineHeat.StreakAfter(s, RedlineThreadGrade.Redhot);
            Assert.AreEqual(2, s);
            Assert.AreEqual(0, RedlineHeat.StreakAfter(s, RedlineThreadGrade.Threaded), "off the heart breaks it");
            Assert.AreEqual(0, RedlineHeat.StreakAfter(s, RedlineThreadGrade.Missed));
            Assert.AreEqual(1, RedlineHeat.StreakAfter(-3, RedlineThreadGrade.Clean));
        }

        [Test]
        public void Streaks_are_called_at_the_tiers_then_every_sixteen()
        {
            var called = new System.Collections.Generic.List<int>();
            for (int s = 0; s <= 64; s++) if (RedlineHeat.IsCalled(s)) called.Add(s);
            Assert.AreEqual("3,8,16,32,48,64", string.Join(",", called));
        }

        [Test]
        public void A_lap_ends_on_its_last_ring()
        {
            Assert.AreEqual(0, RedlineHeat.LapEndedBy(0, 8));
            Assert.AreEqual(0, RedlineHeat.LapEndedBy(7, 8));
            Assert.AreEqual(1, RedlineHeat.LapEndedBy(8, 8));
            Assert.AreEqual(0, RedlineHeat.LapEndedBy(9, 8));
            Assert.AreEqual(8, RedlineHeat.LapEndedBy(64, 8), "the finish is the eighth lap's end");
            Assert.AreEqual(0, RedlineHeat.LapEndedBy(8, 0));
        }

        [Test]
        public void The_crossing_offset_is_where_the_step_meets_the_plane()
        {
            Vector3 centre = new Vector3(10f, 0f, 0f), axis = Vector3.forward;
            Assert.AreEqual(3f, RedlineHeat.CrossingOffset(new Vector3(10f, 3f, -5f), new Vector3(10f, 3f, 5f), centre, axis), 1e-4f);
            Assert.AreEqual(5f, RedlineHeat.CrossingOffset(new Vector3(15f, 0f, 2f), new Vector3(15f, 0f, -2f), centre, axis), 1e-4f,
                "backwards counts, as the race's own test does");
            Assert.IsTrue(float.IsPositiveInfinity(
                RedlineHeat.CrossingOffset(new Vector3(10f, 0f, 1f), new Vector3(10f, 0f, 5f), centre, axis)), "no crossing");
        }

        [Test]
        public void The_redline_needs_Time_ten_and_full_Soar()
        {
            Assert.Greater(RedlineHeat.RedlineSpeed, RedlineCourse.SoarTop(9), "a Time-9 Manta never reaches it");
            Assert.LessOrEqual(RedlineHeat.RedlineSpeed, RedlineCourse.SoarTop(10));
            Assert.Greater(RedlineHeat.RedlineSpeed, RedlineCourse.SoarSpeed(0.95f, 10), "nor an eased one");
        }
    }
}
