using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>A Switchback medal (Plan 3, <c>SwitchbackFunType.Plan3_ParMedals</c>): how a finish time sits against par.</summary>
    public enum SwitchbackMedal
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,
    }

    /// <summary>
    /// Plan 3's par time: what a GOOD Dolphin run of a course takes, computed from the course itself so every
    /// generated course (every seed, every fun type, every intensity) gets a fair target. Pure and scene-free;
    /// <c>SwitchbackFunTypeTests</c> holds it.
    ///
    /// <para><b>The model.</b> A good pilot flies the gate-to-gate polyline (from the cell centre's distance to
    /// gate 1, the lead-in, then every leg) at <see cref="ParSpeed"/> - cruise plus 60% of the way to boost
    /// (68 + 0.6 x (347 - 68) = 235 u/s), because nobody boosts the whole race - and loses the time to swing the
    /// nose through every hairpin at the Dolphin's 110 deg/s. Ordinary corners are flown on the line and cost
    /// nothing extra. The numbers are the Dolphin's measured speeds (SwitchbackCourseTests' boost turning
    /// circle); the 60% share and the medal bands are play-test knobs, not physics.</para>
    /// </summary>
    public static class SwitchbackPar
    {
        public const float DolphinCruiseSpeed = 68f;   // u/s
        public const float DolphinBoostSpeed = 347f;   // u/s
        public const float DolphinTurnRate = 110f;     // deg/s, speed-independent
        public const float BoostShare = 0.6f;
        public static float ParSpeed => DolphinCruiseSpeed + BoostShare * (DolphinBoostSpeed - DolphinCruiseSpeed);

        /// <summary>A hairpin, for par: a corner sharper than this costs the time to turn through it.</summary>
        public const float HairpinCostAboveDegrees = 85f;

        public const float SilverFactor = 1.2f;   // Silver within 20% of par
        public const float BronzeFactor = 1.5f;   // Bronze within 50%

        /// <summary>Par for the WHOLE course: the time to the last gate.</summary>
        public static float ParSeconds(IReadOnlyList<RaceGate> course, Vector3 centre) =>
            course == null || course.Count == 0 ? 0f : ParAtGate(course, centre, course.Count - 1);

        /// <summary>
        /// Par to thread gate <paramref name="gateIndex"/> (0-based): the lead-in, the legs up to it, and the
        /// hairpins turned before it. What the split toasts are measured against.
        /// </summary>
        public static float ParAtGate(IReadOnlyList<RaceGate> course, Vector3 centre, int gateIndex)
        {
            if (course == null || course.Count == 0) return 0f;
            int last = Mathf.Clamp(gateIndex, 0, course.Count - 1);
            float distance = (course[0].Position - centre).magnitude;
            float turnSeconds = 0f;
            for (int i = 1; i <= last; i++)
            {
                distance += (course[i].Position - course[i - 1].Position).magnitude;
                // The corner at gate i-1 (turned before reaching gate i).
                int c = i - 1;
                if (c >= 1)
                {
                    float turn = RaceCourseGeometry.Angle(
                        RaceCourseGeometry.SafeNormalize(course[c].Position - course[c - 1].Position, Vector3.forward),
                        RaceCourseGeometry.SafeNormalize(course[c + 1].Position - course[c].Position, Vector3.forward));
                    if (turn > HairpinCostAboveDegrees) turnSeconds += turn / DolphinTurnRate;
                }
            }
            return distance / ParSpeed + turnSeconds;
        }

        /// <summary>The medal a finish of <paramref name="seconds"/> earns against <paramref name="par"/>.</summary>
        public static SwitchbackMedal MedalFor(float seconds, float par)
        {
            if (par <= 0f || seconds <= 0f) return SwitchbackMedal.None;
            if (seconds <= par) return SwitchbackMedal.Gold;
            if (seconds <= par * SilverFactor) return SwitchbackMedal.Silver;
            if (seconds <= par * BronzeFactor) return SwitchbackMedal.Bronze;
            return SwitchbackMedal.None;
        }

        /// <summary>"+1.4" / "-0.8": a split against its target, signed, one decimal.</summary>
        public static string FormatDelta(float seconds, float target)
        {
            float d = seconds - target;
            return (d >= 0f ? "+" : "-") + Mathf.Abs(d).ToString("0.0");
        }

        /// <summary>"1:02.3" / "48.7": a race time.</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes > 0 ? $"{minutes}:{rest:00.0}" : rest.ToString("0.0");
        }
    }
}
