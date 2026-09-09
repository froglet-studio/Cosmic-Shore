using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ONE definition of "how far off full-speed-straight is this pilot right now", shared by
    /// every <see cref="IInputStrategy"/> that raises <see cref="Data.InputEvents.FullSpeedStraightAction"/>
    /// and by the abilities that answer it.
    ///
    /// <para>It was six identical copies — gamepad, keyboard, keyboard+mouse, touch, dual-mouse and
    /// single-stick-mouse each carried the same three lines and the same <c>0.3f</c>. That was
    /// survivable while the event was a latch nobody measured, and stopped being survivable when
    /// the Rhino's ramp boost started GRADING its output by the same quantity
    /// (<c>RHINO_RAMP_BOOST.md</c>): the ability's full-power plateau has to end exactly where the
    /// gesture engages, and two copies of a number are two numbers.</para>
    ///
    /// <para><b>The deviation is a SUM, so it is not bounded by 1.</b> Full throttle contributes 0
    /// and no throttle contributes 1; each rotation axis contributes its own magnitude, so a pilot
    /// pitching and yawing at once can reach 2. A single-axis turn at full throttle — which is what
    /// a corner is — makes deviation exactly the stick fraction, which is why a turn rate linear in
    /// stick makes the ramp's speed/radius trade analytic.</para>
    /// </summary>
    public static class StraightLineGesture
    {
        /// <summary>
        /// Deviation below which the gesture engages, and (for a graded consumer) the edge of the
        /// FULL-POWER plateau. Every input strategy tests against this one value.
        /// </summary>
        public const float EngageThreshold = 0.3f;

        /// <summary>Total rotational input, summed across the three steering axes.</summary>
        public static float SumOfRotations(IInputStatus s) =>
            Mathf.Abs(s.YDiff) + Mathf.Abs(s.YSum) + Mathf.Abs(s.XSum);

        /// <summary>How far the pilot is from "throttle buried, stick centred". 0 is dead straight.</summary>
        public static float DeviationFromFullSpeedStraight(IInputStatus s) =>
            (1f - s.XDiff) + SumOfRotations(s);

        /// <summary>How far the pilot is from "throttle closed, stick centred". 0 is dead slow.</summary>
        public static float DeviationFromMinimumSpeedStraight(IInputStatus s) =>
            s.XDiff + SumOfRotations(s);
    }
}
