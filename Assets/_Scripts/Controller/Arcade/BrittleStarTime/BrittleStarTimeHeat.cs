using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How the local pilot threaded a Brittle Star Time ring (<see cref="BrittleStarTimeHeat.Grade"/>).</summary>
    public enum BrittleStarThreadGrade
    {
        /// <summary>Crossed the ring's plane outside the mouth.</summary>
        Missed = 0,
        /// <summary>Through the mouth, under the speed the ring's line carries.</summary>
        Threaded = 1,
        /// <summary>A flat-out ring (Speed Mode holds it) taken through the heart carrying its speed:
        /// <see cref="BrittleStarTimeHeat.CarriedShare"/> of full Speed or more, within
        /// <see cref="BrittleStarTimeHeat.HeartShare"/> of the mouth.</summary>
        FlatOut = 2,
        /// <summary>A let-go ring (its corner is tighter than Speed Mode's circle) taken carrying its corner's speed: let
        /// go at the posts, coasted in, not crawled.</summary>
        CleanShift = 3,
    }

    /// <summary>
    /// Brittle Star Time's heat (2026-11-16): what a thread was worth, the streak, the laps. Pure, so the controller's
    /// feedback (<c>BrittleStarTimeController.Heat</c>), its tests and the offline harness read one rule. Local to each
    /// pilot's own machine: nothing here is scored or networked.
    ///
    /// <para><b>A thread is judged by the speed it CARRIED, not by where in the mouth it went.</b> The Brittle Star's
    /// question is Speed against the turn, and some rungs' true line runs off the mouth's heart (the feather slalom is
    /// flown on the half-mouth line), so an offset grade would punish the right line. A ring's line speed is its corner
    /// speed (<see cref="BrittleStarTimeLine.CornerSpeed"/>), full Speed on a flat-out ring: carry
    /// <see cref="CarriedShare"/> of it through the mouth and the thread is FLAT OUT, or a CLEAN SHIFT on a let-go ring.
    /// Every carried thread adds to the streak; a slow thread holds it; a missed mouth ends it. Streaks are called at 3, 8
    /// and 16, then every 16.</para>
    /// </summary>
    public static class BrittleStarTimeHeat
    {
        /// <summary>A thread carried its ring's line speed at this share of it or more.</summary>
        public const float CarriedShare = 0.8f;

        /// <summary>A FLAT-OUT thread also goes through the heart: within this share of the mouth's radius. Wide enough
        /// for the feather slalom's half-mouth line; a let-go ring asks only for the speed (its line runs to the rim).</summary>
        public const float HeartShare = 0.6f;

        /// <summary>The streak lengths that are called (and every <see cref="StreakRepeat"/> past the last).</summary>
        public static readonly int[] StreakTiers = { 3, 8, 16 };

        public const int StreakRepeat = 16;

        /// <summary>Grade a thread of ring <paramref name="ring"/> that crossed <paramref name="offset"/> u from the mouth's
        /// centre at <paramref name="speed"/>, the hull's full Speed being <paramref name="topSpeed"/>.</summary>
        public static BrittleStarThreadGrade Grade(IReadOnlyList<RaceGate> lap, int ring, float offset, float speed, float topSpeed)
        {
            var g = lap[ring];
            if (offset > g.Radius) return BrittleStarThreadGrade.Missed;
            float line = BrittleStarTimeLine.CornerSpeed(lap, ring, topSpeed);
            if (speed < CarriedShare * line) return BrittleStarThreadGrade.Threaded;
            if (BrittleStarTimeLine.LetGoDistance(lap, ring, topSpeed) > 0f) return BrittleStarThreadGrade.CleanShift;
            return offset <= HeartShare * g.Radius ? BrittleStarThreadGrade.FlatOut : BrittleStarThreadGrade.Threaded;
        }

        /// <summary>The streak after a thread graded <paramref name="grade"/>: a carried thread adds one, a slow thread
        /// holds it, a missed mouth ends it.</summary>
        public static int StreakAfter(int streak, BrittleStarThreadGrade grade) => grade switch
        {
            BrittleStarThreadGrade.Missed => 0,
            BrittleStarThreadGrade.Threaded => Mathf.Max(0, streak),
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

        /// <summary>The lap (1-based) a pilot who has threaded <paramref name="threaded"/> rings of a lap of
        /// <paramref name="ringsPerLap"/> just completed, or 0 when that thread did not end a lap.</summary>
        public static int LapEndedBy(int threaded, int ringsPerLap) =>
            ringsPerLap > 0 && threaded > 0 && threaded % ringsPerLap == 0 ? threaded / ringsPerLap : 0;

        /// <summary>How far from the mouth's centre the segment <paramref name="prev"/> to <paramref name="cur"/> crosses
        /// the ring's plane (<paramref name="centre"/>, <paramref name="axis"/>), either way; infinity when it does not.</summary>
        public static float CrossingOffset(Vector3 prev, Vector3 cur, Vector3 centre, Vector3 axis)
        {
            float a = Vector3.Dot(prev - centre, axis), b = Vector3.Dot(cur - centre, axis);
            if (a == 0f && b == 0f) return float.PositiveInfinity;
            if (a * b > 0f) return float.PositiveInfinity;
            float t = a / (a - b);
            Vector3 hit = prev + (cur - prev) * t;
            return (hit - centre).magnitude;
        }

        /// <summary>A plane crossing counts as an attempt at the ring (a thread, or a miss) only within this many mouth
        /// radii of its centre.</summary>
        public const float AttemptRadii = 2.5f;

        /// <summary>Is a crossing <paramref name="offset"/> u from a mouth of <paramref name="radius"/> an attempt at the
        /// ring? Further out it is flight past the plane, not at the ring (an apex ring's plane runs along the leg into
        /// it), and is neither graded nor counted as a miss.</summary>
        public static bool IsAnAttempt(float offset, float radius) =>
            !float.IsPositiveInfinity(offset) && offset <= AttemptRadii * radius;

        /// <summary>Has the pilot been dark (out of Speed Mode) long enough at the start that the hint is worth repeating?
        /// Once, after <see cref="SpeedDarkSeconds"/> of the turn with no ring threaded and Speed Mode never lit.</summary>
        public static bool SpeedStillDark(float turnSeconds, int threaded, bool everLit) =>
            !everLit && threaded == 0 && turnSeconds >= SpeedDarkSeconds;

        /// <summary>Seconds of a dark start before the Speed Mode reminder (the recorded I1 pilot crawled 41 s to ring 1).</summary>
        public const float SpeedDarkSeconds = 6f;
    }
}
