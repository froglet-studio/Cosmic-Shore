using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The mouse-delta → virtual-stick integration used by
    /// <see cref="SingleStickMouseInputStrategy"/>, pulled out as pure math for the same reason
    /// <see cref="DualStickMix"/> was: it is the load-bearing half of a control scheme, and a
    /// control scheme that can only be checked by flying it cannot be checked at all. Every claim
    /// below is asserted in <c>MouseVirtualStickTests</c>.
    ///
    /// <para>A mouse reports how far it MOVED; a one-thumb vessel asks how far the stick is
    /// PUSHED, and answers that with a TURN RATE. The gap between those is this function.</para>
    ///
    /// <para><b>The spring is proportional and always on, and that is the whole design.</b> The
    /// first cut sprang back linearly and only on frames where the mouse was still, which is
    /// unflyable in a way no "does it return to centre" test can see: the spring was off whenever
    /// you were actually steering, so ANY drag wound up pinned at full deflection and no stable
    /// partial turn existed anywhere. Proportional-and-always-on gives the mapping a player can
    /// fly — <b>how fast you move the mouse is how hard the vessel turns</b> — with
    /// <see cref="SustainedDeflection"/> as its exact control curve.</para>
    ///
    /// <para><b>The step is the closed-form solution, not a per-frame approximation.</b> Treating
    /// the frame's delta as a constant rate makes <c>ds/dt = v·k − spring·s</c> exactly solvable
    /// over the frame, and using that solution buys two things a naive
    /// "integrate then multiply by exp" does not: the sustained deflection is EXACTLY
    /// <c>v·k/spring</c> at any frame rate (the naive form settles a few percent low, and by a
    /// frame-rate-dependent amount), and the SUSTAINED curve is identical at 30 and 240 fps —
    /// measured to 0.002 of a stick unit across 30 / 60 / 144 / 240.</para>
    ///
    /// <para>What is deliberately NOT frame-rate independent is a single-frame flick, which
    /// lands at <c>pixels × unitsPerPixel × (1 − e) / (spring × dt)</c> — about 3% short at
    /// 60 fps and 6% at 30. That is the spring correctly acting on the deflection during the
    /// frame it was applied, not an artefact: a real flick spans several frames and settles onto
    /// the exact curve, and the alternative is a scheme whose steady state drifts with frame
    /// rate, which is the one the player would actually feel.</para>
    ///
    /// <para><b>The dead zone is applied to the OUTPUT, never to the state</b> —
    /// see <see cref="Deflection"/>. Snapping the accumulator itself is a RATCHET: at the shipped
    /// numbers a 60 fps frame under ~110 px/s adds less than the dead zone, so it was zeroed
    /// every frame and could never accumulate. Slow, careful mouse movement — precisely what
    /// aiming is made of — did nothing at all, and the speed it took to escape scaled with frame
    /// rate. Keeping the state honest and hiding only the report is what makes a slow drag a slow
    /// turn.</para>
    ///
    /// <para><c>springPerSecond = 0</c> disables the spring entirely, leaving a pure accumulator:
    /// push once and the vessel keeps turning until you push back, which is what
    /// <c>DualMouseInputStrategy</c> effectively does. That is the other school of mouse flight
    /// and it is one field away, not a rewrite.</para>
    /// </summary>
    public static class MouseVirtualStick
    {
        /// <summary>
        /// Below this <c>springPerSecond × deltaTime</c> the closed form's <c>(1 − e) / spring</c>
        /// loses precision against its own limit (<c>deltaTime</c>), so the no-spring branch is
        /// taken instead. It is a numerical guard, not a feel threshold.
        /// </summary>
        const float MinSpringStep = 1e-5f;

        /// <summary>
        /// Advance the virtual stick by one frame of mouse movement. The result is the stick's
        /// STATE — pass it through <see cref="Deflection"/> before publishing it.
        /// </summary>
        /// <param name="stick">The stick's current state, from the previous call.</param>
        /// <param name="pixelDelta">This frame's mouse delta, in pixels.</param>
        /// <param name="unitsPerPixel">Stick units gained per pixel of movement.</param>
        /// <param name="springPerSecond">Exponential return rate toward centre, in reciprocal
        /// seconds. 0 disables the spring.</param>
        /// <param name="deltaTime">Frame time.</param>
        public static Vector2 Step(Vector2 stick, Vector2 pixelDelta, float unitsPerPixel,
                                   float springPerSecond, float deltaTime)
        {
            float springStep = springPerSecond * deltaTime;

            if (springStep > MinSpringStep)
            {
                float decay = Mathf.Exp(-springStep);
                // pixelDelta / deltaTime is this frame's drag RATE; (1 - decay) / spring is the
                // exact integral of the forcing term across the frame. Written as one factor so
                // deltaTime cancels rather than appearing twice.
                float forcing = (1f - decay) / springPerSecond;
                stick = stick * decay + pixelDelta * (unitsPerPixel * forcing / deltaTime);
            }
            else
            {
                // No spring (or a degenerate frame time): a pure accumulator, which is also the
                // exact springPerSecond → 0 limit of the branch above.
                stick += pixelDelta * unitsPerPixel;
            }

            // Clamp to the UNIT CIRCLE, not per axis. LeftNormalizedJoystickPosition is read as a
            // RADIUS by BarrelRollController (|stick| >= perimeterThreshold arms the Sparrow's
            // strafing roll) and by ScarabJukeController, so the magnitude has to be able to
            // reach exactly 1 and must never exceed it. Clamping the STATE (not just the report)
            // is also what stops a long sweep banking deflection the player then has to unwind.
            return Vector2.ClampMagnitude(stick, 1f);
        }

        /// <summary>
        /// What to publish for a given stick state: the state itself, or exactly centred once it
        /// is inside the dead zone. The exponential only ever APPROACHES zero, so this is what
        /// actually lands on it — without it the vessel carries a permanent sub-perceptual turn,
        /// which reads as drift rather than as a control.
        /// </summary>
        public static Vector2 Deflection(Vector2 stick, float deadZone)
            => stick.sqrMagnitude < deadZone * deadZone ? Vector2.zero : stick;

        /// <summary>
        /// The deflection a sustained drag of <paramref name="pixelsPerSecond"/> settles at —
        /// the scheme's real control curve, and the thing to reason about when retuning
        /// <see cref="CosmicShore.ScriptableObjects.MouseFlightConfigSO"/>, since neither field
        /// means anything on its own. Returns 1 (pinned) when there is no spring to balance
        /// against.
        /// </summary>
        public static float SustainedDeflection(float pixelsPerSecond, float unitsPerPixel,
                                                float springPerSecond)
        {
            if (springPerSecond <= 0f) return 1f;
            return Mathf.Min(1f, pixelsPerSecond * unitsPerPixel / springPerSecond);
        }
    }
}
