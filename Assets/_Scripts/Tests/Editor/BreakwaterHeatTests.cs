using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Breakwater's heat (<see cref="BreakwaterHeat"/>, BREAKWATER.md "The heat"): a station is graded by the verb the
    /// pilot chose, the streak counts eyes, and a lap is the circuit without the start gate. Runs offline too:
    /// Tools/Build/breakwater_ruins_harness/run.sh.
    /// </summary>
    public class BreakwaterHeatTests
    {
        const float Eye = BreakwaterCourseSettings.EyeRadius;

        [Test]
        public void A_pass_is_graded_by_where_it_crossed_and_the_afterburner()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
            {
                float port = BreakwaterCourseSettings.PortRadiusForIntensity(intensity);
                Assert.Greater(port, Eye, "the port is wider than the eye on every rung, so a door is a grade of its own");
                Assert.AreEqual(BreakwaterThreadGrade.Eye, BreakwaterHeat.Grade(0f, port, false));
                Assert.AreEqual(BreakwaterThreadGrade.Eye, BreakwaterHeat.Grade(Eye, port, false));
                Assert.AreEqual(BreakwaterThreadGrade.HotEye, BreakwaterHeat.Grade(Eye, port, true));
                Assert.AreEqual(BreakwaterThreadGrade.Door, BreakwaterHeat.Grade(Eye + 0.1f, port, true),
                                "the afterburner through a door is still a door");
                Assert.AreEqual(BreakwaterThreadGrade.Door, BreakwaterHeat.Grade(port, port, false));
                Assert.AreEqual(BreakwaterThreadGrade.Missed, BreakwaterHeat.Grade(port + 0.1f, port, true));
            }
        }

        [Test]
        public void An_eye_adds_to_the_streak_a_door_holds_it_a_miss_ends_it()
        {
            Assert.AreEqual(1, BreakwaterHeat.StreakAfter(0, BreakwaterThreadGrade.Eye));
            Assert.AreEqual(5, BreakwaterHeat.StreakAfter(4, BreakwaterThreadGrade.HotEye));
            Assert.AreEqual(4, BreakwaterHeat.StreakAfter(4, BreakwaterThreadGrade.Door));
            Assert.AreEqual(0, BreakwaterHeat.StreakAfter(4, BreakwaterThreadGrade.Missed));
        }

        [Test]
        public void Streaks_are_called_at_3_7_14_then_every_14()
        {
            int called = 0;
            for (int s = -1; s <= 42; s++)
            {
                bool expect = s == 3 || s == 7 || s == 14 || s == 28 || s == 42;
                Assert.AreEqual(expect, BreakwaterHeat.IsCalled(s), $"streak {s}");
                if (expect) called++;
            }
            Assert.AreEqual(5, called);
        }

        [Test]
        public void A_lap_is_the_circuit_without_the_start_gate()
        {
            // The shipped race: 15 rings, the first a start gate flown once, two laps = 29 crossings.
            const int rings = 15, leadIn = 1;
            Assert.AreEqual(0, BreakwaterHeat.LapEndedBy(1, rings, leadIn), "the start gate ends no lap");
            Assert.AreEqual(0, BreakwaterHeat.LapEndedBy(14, rings, leadIn));
            Assert.AreEqual(1, BreakwaterHeat.LapEndedBy(15, rings, leadIn));
            Assert.AreEqual(0, BreakwaterHeat.LapEndedBy(16, rings, leadIn));
            Assert.AreEqual(2, BreakwaterHeat.LapEndedBy(29, rings, leadIn));
            // No lead-in: the plain fold.
            Assert.AreEqual(1, BreakwaterHeat.LapEndedBy(15, 15, 0));
            Assert.AreEqual(0, BreakwaterHeat.LapEndedBy(0, 15, 0));
            Assert.AreEqual(0, BreakwaterHeat.LapEndedBy(3, 1, 1), "a course that is all lead-in has no laps");
        }

        [Test]
        public void The_crossing_offset_is_measured_on_the_stations_plane()
        {
            var centre = new Vector3(100f, 0f, 0f);
            var axis = Vector3.forward;
            // Straight through, 10 u off the axis.
            Assert.AreEqual(10f, BreakwaterHeat.CrossingOffset(new Vector3(110f, 0f, -5f), new Vector3(110f, 0f, 5f), centre, axis), 1e-3f);
            // Backward counts too: the race credits either way.
            Assert.AreEqual(10f, BreakwaterHeat.CrossingOffset(new Vector3(110f, 0f, 5f), new Vector3(110f, 0f, -5f), centre, axis), 1e-3f);
            // Never reaches the plane.
            Assert.IsTrue(float.IsPositiveInfinity(
                BreakwaterHeat.CrossingOffset(new Vector3(100f, 0f, -9f), new Vector3(100f, 0f, -1f), centre, axis)));
            // Sliding in the plane is not a crossing.
            Assert.IsTrue(float.IsPositiveInfinity(
                BreakwaterHeat.CrossingOffset(new Vector3(100f, 0f, 0f), new Vector3(120f, 0f, 0f), centre, axis)));
        }

        [Test]
        public void Flight_far_past_the_plane_is_not_an_attempt()
        {
            float port = BreakwaterCourseSettings.PortRadiusForIntensity(4);
            Assert.IsTrue(BreakwaterHeat.IsAnAttempt(port + 1f, port), "a near miss is a miss");
            Assert.IsTrue(BreakwaterHeat.IsAnAttempt(BreakwaterHeat.AttemptRadii * port, port));
            Assert.IsFalse(BreakwaterHeat.IsAnAttempt(BreakwaterHeat.AttemptRadii * port + 1f, port));
            Assert.IsFalse(BreakwaterHeat.IsAnAttempt(float.PositiveInfinity, port));
        }
    }
}
