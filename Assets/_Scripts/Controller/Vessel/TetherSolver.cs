using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Gibbon's tether physics, as PURE FUNCTIONS of state — no Unity objects, no side
    /// effects, no <c>Time.deltaTime</c> read. Everything here is driven by the offline harness
    /// (<c>Tools/Build/gibbon_swing.py</c> mirrors it; <c>TetherSolverTests</c> asserts the shipped
    /// C#), because a swing that explodes, ratchets, or gains free energy is invisible in review
    /// and obvious in one graph.
    ///
    /// <b>The line is a ROPE, not a spring.</b> Slack does nothing at all: force exists only past
    /// the rest length. That single asymmetry is what makes the mechanic read as a tether rather
    /// than a rubber band glued to a rock.
    ///
    /// <b>Why force and not a constraint.</b> The previous attempt at this vessel solved a RIGID
    /// rope — one exact ray/sphere snap, exact circle stepping, and a closed-form intersection
    /// circle for the two-line case. It was correct and it was ~1,700 lines, because a rigid
    /// constraint has to answer "is this configuration even feasible?" and a force never does.
    /// Two forces just ADD. The whole two-tether case is <see cref="Vector3"/> addition here.
    /// </summary>
    public static class TetherSolver
    {
        /// <summary>Below this, a length or a direction is numerically meaningless.</summary>
        public const float Epsilon = 1e-4f;

        /// <summary>What one line did to the hull this frame.</summary>
        public readonly struct LineStep
        {
            /// <summary>Velocity change from this line, ALREADY multiplied by dt (the same
            /// convention as <c>VesselTransformer.ComputeNoseAcceleration</c>).</summary>
            public readonly Vector3 DeltaV;

            /// <summary>0 while slack, rising to 1 at <c>maxStretch</c>. The line's visual
            /// thickness and the HUD both read this, so tension is never a second opinion.</summary>
            public readonly float Tension01;

            /// <summary>The line was stretched past <c>breakStretch</c> and must be released.
            /// This is BOTH the game rule ("overload it and it snaps") and the stability
            /// guarantee — it is what bounds how far the solver can ever be from equilibrium.</summary>
            public readonly bool Break;

            public LineStep(Vector3 deltaV, float tension01, bool brk)
            {
                DeltaV = deltaV; Tension01 = tension01; Break = brk;
            }

            public static readonly LineStep Slack = new LineStep(Vector3.zero, 0f, false);
        }

        /// <summary>
        /// One line's contribution for one frame.
        ///
        /// <b>Spring</b>: <c>-k·min(stretch, maxStretch)·dir</c>. Explicit, and stable because the
        /// stretch fed to it is CLAMPED — so the acceleration this can ever produce is bounded by
        /// <c>stiffness × maxStretch</c>, a number that can be stated rather than hoped for. A real
        /// rope does not produce unbounded tension either; it breaks, which is the next field.
        ///
        /// <b>Damper</b>: applied to the RADIAL component only, and applied as the EXACT solution
        /// of <c>dv/dt = −c·v</c> (<c>v·e^(−c·dt)</c>) rather than <c>v − c·v·dt</c>. Two reasons,
        /// both of which have bitten this codebase before (see the drag note in
        /// <c>VesselTransformer</c>): the exponential is unconditionally stable, so a hitch frame
        /// cannot overshoot into negative damping and inject energy; and it is frame-rate
        /// identical, so a 30 fps machine and a 120 fps machine swing the same arc.
        ///
        /// <b>The tangential component is never touched.</b> That is the whole design. A rope does
        /// no tangential work, so swing speed is preserved, and angular momentum about the anchor
        /// is conserved for free — which is what turns reeling in into a speed-up with no "pump"
        /// formula anywhere. Damping the full velocity instead would brake the swing, which is the
        /// single easiest way to make this vessel feel like mud.
        /// </summary>
        public static LineStep Step(
            Vector3 hullPosition, Vector3 velocity, Vector3 anchor, float restLength,
            float stiffness, float dampingPerSecond, float maxStretch, float breakStretch, float dt)
        {
            Vector3 d = hullPosition - anchor;
            float len = d.magnitude;
            if (len < Epsilon) return LineStep.Slack;

            float stretch = len - restLength;
            if (stretch <= 0f) return LineStep.Slack;          // rope: slack is silent

            Vector3 dir = d / len;
            bool brk = breakStretch > 0f && stretch > breakStretch;

            float clamped = maxStretch > 0f ? Mathf.Min(stretch, maxStretch) : stretch;
            Vector3 deltaV = -dir * (stiffness * clamped * dt);

            float radial = Vector3.Dot(velocity, dir);
            float dampedRadial = radial * Mathf.Exp(-Mathf.Max(0f, dampingPerSecond) * dt);
            deltaV += dir * (dampedRadial - radial);

            float tension = maxStretch > Epsilon ? Mathf.Clamp01(stretch / maxStretch) : 1f;
            return new LineStep(deltaV, tension, brk);
        }

        /// <summary>
        /// How far the winch shortens a line this frame.
        ///
        /// The reel is the vessel's ONLY energy source, so this is the one place a speed cap can
        /// be enforced honestly. Efficiency falls as <c>1 − (speed/cap)²</c> and reaches zero AT
        /// the cap: the winch simply stops doing work, rather than a brake being applied to a
        /// pilot who earned their speed. A vessel can still be carrying more than <c>cap</c> —
        /// from a boost, a blast, another pilot — and nothing here punishes that; it just will not
        /// ADD to it.
        ///
        /// Squaring matters: linear falloff leaves usable pull right up to the cap and the cap
        /// then reads as a wall. Squared, the last fifth of the range is visibly soft, which is
        /// what makes the ceiling feel like a terminal velocity instead of a rule.
        /// </summary>
        public static float ReelStep(float speed, float speedCap, float reelRatePerSecond, float dt)
        {
            if (reelRatePerSecond <= 0f || dt <= 0f) return 0f;
            float efficiency = speedCap > Epsilon
                ? Mathf.Clamp01(1f - (speed * speed) / (speedCap * speedCap))
                : 1f;
            return reelRatePerSecond * efficiency * dt;
        }

        /// <summary>
        /// How long a beam fired at analog depth <paramref name="depth01"/> reaches.
        ///
        /// Deliberately LINEAR in the trigger: the pilot is aiming a distance they can see drawn
        /// ahead of them, and any curve here makes the drawn preview and the felt control
        /// disagree. The preview IS the contract.
        /// </summary>
        public static float BeamLength(float depth01, float minLength, float maxLength)
            => Mathf.Lerp(minLength, maxLength, Mathf.Clamp01(depth01));

        /// <summary>
        /// The floor a line may be reeled to: never inside the hull, never inside the anchor, and
        /// never so short that the orbital rate exceeds <paramref name="maxSwingRateRadPerSec"/>.
        ///
        /// That last term is what stops the end of a reel becoming a blur. Tangential speed is
        /// <c>ω·r</c> and angular momentum is conserved, so as r falls ω climbs as 1/r² — the
        /// camera cannot follow it and the pilot cannot read it. Bounding ω instead of r makes the
        /// floor a function of how fast you are ACTUALLY going, so a slow swing may reel much
        /// tighter than a fast one.
        ///
        /// <b>The floor stops a reel; it never pays a line out.</b> The previous attempt learned
        /// this one the hard way: raising the floor as the pump raised the orbital speed, and then
        /// LENGTHENING any line that sat below the floor of the moment, chattered between reel and
        /// pay-out at every frame rate. A line is allowed to be shorter than the current floor.
        /// </summary>
        public static float RestLengthFloor(
            float hullRadius, float anchorRadius, float clearance,
            float tangentialSpeed, float maxSwingRateRadPerSec)
        {
            float geometric = hullRadius + anchorRadius + clearance;
            float kinematic = maxSwingRateRadPerSec > Epsilon
                ? tangentialSpeed / maxSwingRateRadPerSec
                : 0f;
            return Mathf.Max(geometric, kinematic);
        }

        /// <summary>
        /// Apply one frame of winch travel to a rest length, honouring the floor.
        ///
        /// <b>THIS EXISTS BECAUSE THE OBVIOUS ONE-LINER IS WRONG.</b>
        /// <c>rest = Max(floor, rest − step)</c> reads correctly and LENGTHENS the line whenever
        /// the floor has risen above it — and the floor rises with orbital speed, which the reel
        /// itself is raising. The result is a line that reels in, is pushed back out, reels in
        /// again, at a rate that changes with the frame rate. The previous build of this vessel
        /// shipped that and the fix is written into its notes as a rule: <i>the floor STOPS a
        /// reel, it never pays a line out.</i> Putting the rule here rather than at the call site
        /// is the difference between fixing it once and fixing it in every future caller — the
        /// harness's T9 is a standing negative control on exactly that.
        ///
        /// A line is therefore ALLOWED to sit below the floor of the moment: it got there
        /// legitimately, by being reeled while slow, and then speeding up.
        /// </summary>
        public static float ApplyReel(float restLength, float reelStep, float floorLength)
            => Mathf.Min(restLength, Mathf.Max(restLength - reelStep, floorLength));

        /// <summary>The part of <paramref name="velocity"/> that is ACROSS the line — the swing
        /// itself, and the term <see cref="RestLengthFloor"/> bounds.</summary>
        public static float TangentialSpeed(Vector3 hullPosition, Vector3 velocity, Vector3 anchor)
        {
            Vector3 d = hullPosition - anchor;
            float len = d.magnitude;
            if (len < Epsilon) return velocity.magnitude;
            Vector3 dir = d / len;
            return (velocity - dir * Vector3.Dot(velocity, dir)).magnitude;
        }
    }
}
