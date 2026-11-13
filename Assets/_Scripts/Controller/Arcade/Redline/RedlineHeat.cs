using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How the local pilot threaded a Redline gate (<see cref="RedlineHeat.Grade"/>).</summary>
    public enum RedlineThreadGrade
    {
        /// <summary>Crossed the gate's plane outside the mouth.</summary>
        Missed = 0,
        /// <summary>Inside the mouth, off its heart.</summary>
        Threaded = 1,
        /// <summary>Through the heart of the mouth (<see cref="RedlineHeat.CleanMouthFraction"/>).</summary>
        Clean = 2,
        /// <summary>Through the heart at near-full Soar (<see cref="RedlineHeat.RedhotSoarFraction"/>).</summary>
        Redhot = 3,
    }

    /// <summary>
    /// Redline's heat: what a thread was worth, the streak of clean threads, and the race's one REDLINE moment. Pure, so
    /// the controller's feedback (<c>RedlineController.Reading</c>) and its tests read the same rules. Local to each
    /// pilot's own machine: nothing here is scored or networked, it is how the race tells a pilot they flew it well.
    ///
    /// <para>A thread is CLEAN through the heart of the mouth (half its radius) and REDHOT when it is clean at 80% of the
    /// Manta's full Soar for its Time level - a flat-out gate taken dead centre. Every clean thread adds to the streak and
    /// anything else (a thread off the heart, a missed mouth) ends it; the streak is called at 3, 8 and 16 and every 16
    /// after. The redline is 97% of a Time-10 Manta's full Soar (1362 u/s): only a pilot who took the Time crystals AND
    /// kept Soar buried reaches it, once a race.</para>
    /// </summary>
    public static class RedlineHeat
    {
        /// <summary>A thread is clean within this fraction of the mouth's radius.</summary>
        public const float CleanMouthFraction = 0.5f;

        /// <summary>A clean thread is redhot at this fraction of full Soar (<see cref="RedlineCourse.SoarTop"/>).</summary>
        public const float RedhotSoarFraction = 0.8f;

        /// <summary>The redline is this fraction of a Time-10 Manta's full Soar.</summary>
        public const float RedlineFraction = 0.97f;

        /// <summary>The streak lengths that are called (and every <see cref="StreakRepeat"/> past the last).</summary>
        public static readonly int[] StreakTiers = { 3, 8, 16 };

        public const int StreakRepeat = 16;

        /// <summary>1362 u/s: reachable only at Time 9-10 with both triggers buried.</summary>
        public static float RedlineSpeed => RedlineFraction * RedlineCourse.SoarTop(RedlineCourse.MantaMaxTimeLevel);

        /// <summary>Grade a crossing <paramref name="offset"/> u from the centre of a mouth of
        /// <paramref name="mouthRadius"/>, flown at <paramref name="speed"/> by a Manta at <paramref name="timeLevel"/>.</summary>
        public static RedlineThreadGrade Grade(float offset, float mouthRadius, float speed, int timeLevel)
        {
            if (offset > mouthRadius) return RedlineThreadGrade.Missed;
            if (offset > CleanMouthFraction * mouthRadius) return RedlineThreadGrade.Threaded;
            return RedlineCourse.SoarLit(timeLevel) && speed >= RedhotSoarFraction * RedlineCourse.SoarTop(timeLevel)
                ? RedlineThreadGrade.Redhot
                : RedlineThreadGrade.Clean;
        }

        /// <summary>The streak after a thread graded <paramref name="grade"/>.</summary>
        public static int StreakAfter(int streak, RedlineThreadGrade grade) =>
            grade >= RedlineThreadGrade.Clean ? Mathf.Max(0, streak) + 1 : 0;

        /// <summary>Is a streak of <paramref name="streak"/> one that is called?</summary>
        public static bool IsCalled(int streak)
        {
            if (streak <= 0) return false;
            for (int i = 0; i < StreakTiers.Length; i++) if (streak == StreakTiers[i]) return true;
            int last = StreakTiers[StreakTiers.Length - 1];
            return streak > last && (streak - last) % StreakRepeat == 0;
        }

        /// <summary>The lap (1-based) a pilot who has threaded <paramref name="threaded"/> gates of a lap of
        /// <paramref name="ringsPerLap"/> just completed, or 0 when that thread did not end a lap.</summary>
        public static int LapEndedBy(int threaded, int ringsPerLap) =>
            ringsPerLap > 0 && threaded > 0 && threaded % ringsPerLap == 0 ? threaded / ringsPerLap : 0;

        /// <summary>
        /// How far from the mouth's centre the segment <paramref name="prev"/> to <paramref name="cur"/> crosses the gate's
        /// plane (<paramref name="centre"/>, <paramref name="axis"/>); infinity when it does not cross it.
        /// </summary>
        public static float CrossingOffset(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 axis)
        {
            float a = Vector3.Dot(prev - centre, axis), b = Vector3.Dot(cur - centre, axis);
            if ((a > 0f && b > 0f) || (a < 0f && b < 0f) || Mathf.Approximately(a, b)) return float.PositiveInfinity;
            float t = a / (a - b);
            return Vector3.Distance(Vector3.Lerp(prev, cur, t), centre);
        }
    }
}
