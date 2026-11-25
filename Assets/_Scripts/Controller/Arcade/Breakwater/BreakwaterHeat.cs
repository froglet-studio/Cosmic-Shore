using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How the local pilot got through a Breakwater station (<see cref="BreakwaterHeat.Grade"/>).</summary>
    public enum BreakwaterThreadGrade
    {
        /// <summary>Crossed the station's plane outside the port.</summary>
        Missed = 0,
        /// <summary>Through the port outside the eye: a door that was fired or sawn open.</summary>
        Door = 1,
        /// <summary>Through the eye, the 18 u gap that costs nothing.</summary>
        Eye = 2,
        /// <summary>Through the eye with the afterburner lit.</summary>
        HotEye = 3,
    }

    /// <summary>
    /// Breakwater's heat (2026-11-23, elemental loop step 1): what a station was worth to the pilot who flew it, the
    /// streak and the laps. Pure, so the controller's feedback (<c>BreakwaterController.Heat</c>) and its tests read one
    /// rule. Local to each pilot's own machine: nothing here is scored or networked, and the vessel is untouched.
    ///
    /// <para><b>The grade is the verb the pilot chose.</b> Fire and saw both end in a pass through the port outside the
    /// eye, and their reward is the door itself (the blast, the rounds, the prisms that buy the next rocket). Threading
    /// the eye is the choice that pays in nothing the pilot can see, so that is what the heat calls: a thread through
    /// the eye, and a hot one when the afterburner (the Sparrow's Time ability) was lit as it went through. Every eye
    /// adds to the streak; a door holds it; a missed port ends it.</para>
    /// </summary>
    public static class BreakwaterHeat
    {
        /// <summary>The streak lengths that are called (and every <see cref="StreakRepeat"/> past the last). A lap is
        /// 14 stations, so the last tier is a whole lap through the eye.</summary>
        public static readonly int[] StreakTiers = { 3, 7, 14 };

        public const int StreakRepeat = 14;

        /// <summary>A plane crossing counts as an attempt at the station (a pass, or a miss) only within this many port
        /// radii of its centre. Further out it is flight past the plane, not at the station.</summary>
        public const float AttemptRadii = 2.5f;

        /// <summary>Grade a pass that crossed the station's plane <paramref name="offset"/> u from its axis, through a
        /// port of <paramref name="portRadius"/>, with the afterburner lit or not.</summary>
        public static BreakwaterThreadGrade Grade(float offset, float portRadius, bool boosting)
        {
            if (offset > portRadius) return BreakwaterThreadGrade.Missed;
            if (offset > BreakwaterCourseSettings.EyeRadius) return BreakwaterThreadGrade.Door;
            return boosting ? BreakwaterThreadGrade.HotEye : BreakwaterThreadGrade.Eye;
        }

        /// <summary>The streak after a pass graded <paramref name="grade"/>: an eye adds one, a door holds it, a missed
        /// port ends it.</summary>
        public static int StreakAfter(int streak, BreakwaterThreadGrade grade) => grade switch
        {
            BreakwaterThreadGrade.Missed => 0,
            BreakwaterThreadGrade.Door => Mathf.Max(0, streak),
            _ => Mathf.Max(0, streak) + 1,
        };

        /// <summary>Is a streak of <paramref name="streak"/> one that is called?</summary>
        public static bool IsCalled(int streak)
        {
            if (streak <= 0) return false;
            for (int i = 0; i < StreakTiers.Length; i++) if (streak == StreakTiers[i]) return true;
            int last = StreakTiers[StreakTiers.Length - 1];
            return streak > last && (streak - last) % StreakRepeat == 0;
        }

        /// <summary>The lap (1-based) a pilot who has made <paramref name="threaded"/> crossings just completed, on a
        /// course of <paramref name="ringCount"/> rings whose first <paramref name="leadIn"/> are flown once and are
        /// not part of a lap; 0 when that crossing did not end a lap.</summary>
        public static int LapEndedBy(int threaded, int ringCount, int leadIn)
        {
            int perLap = ringCount - leadIn, flown = threaded - leadIn;
            return perLap > 0 && flown > 0 && flown % perLap == 0 ? flown / perLap : 0;
        }

        /// <summary>How far from the station's axis the segment <paramref name="prev"/> to <paramref name="cur"/>
        /// crosses its plane (<paramref name="centre"/>, <paramref name="axis"/>), either way; infinity when it does
        /// not.</summary>
        public static float CrossingOffset(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 axis)
        {
            float a = Vector3.Dot(prev - centre, axis), b = Vector3.Dot(cur - centre, axis);
            if (a == 0f && b == 0f) return float.PositiveInfinity;
            if (a * b > 0f) return float.PositiveInfinity;
            float t = a / (a - b);
            Vector3 hit = prev + (cur - prev) * t;
            return (hit - centre).magnitude;
        }

        /// <summary>Is a crossing <paramref name="offset"/> u from a port of <paramref name="portRadius"/> an attempt
        /// at the station?</summary>
        public static bool IsAnAttempt(float offset, float portRadius) =>
            !float.IsPositiveInfinity(offset) && offset <= AttemptRadii * portRadius;
    }
}
