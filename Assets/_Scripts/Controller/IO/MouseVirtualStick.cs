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
    /// <para><b>The spring is proportional and always on INSIDE the hold band, and dead outside
    /// it. That two-regime shape is the whole design.</b></para>
    ///
    /// <list type="bullet">
    /// <item><b>Near centre it is a RATE stick.</b> A sustained drag of <c>v</c> px/s settles at
    /// <c>v · k / spring</c> (<see cref="SustainedDeflection"/>) and lets go back to centre with
    /// time constant <c>1 / spring</c>. That is what makes small, careful movement a small,
    /// self-correcting turn — the regime aiming lives in, and the reason the vessel flies straight
    /// again when you take your hand off the mouse.</item>
    /// <item><b>Out at the rim it is a POSITION stick.</b> Above
    /// <see cref="SpringScaleAtRadius"/>'s <c>holdOuter</c> the spring is exactly ZERO, so a
    /// committed sweep parks the stick in that annulus and it stays there — the vessel keeps
    /// turning at that rate indefinitely with the mouse dead still. This is the bounded-cursor
    /// model every mouse-flight game that ships one uses (Freelancer's clamped reticle, Elite's
    /// mouse widget, War Thunder's mouse aim), and it is what a rate stick structurally cannot
    /// do: under a pure spring, holding a hard turn costs <c>spring / k</c> px/s FOREVER, so a
    /// 180° costs hundreds of pixels of desk and you run out of mousepad before you run out of
    /// turn.</item>
    /// </list>
    ///
    /// <para>The two meet across a smoothstep, so there is no step in feel at the boundary and no
    /// oscillation across it: the spring only ever gets stronger as the stick falls inward, so
    /// the drift is monotone. The only stable resting places are centred and the annulus, which
    /// is exactly the claim — <i>drift back, or commit</i>.</para>
    ///
    /// <para><b>The step is the closed-form solution, not a per-frame approximation.</b> Treating
    /// the frame's delta as a constant rate makes <c>ds/dt = v·k − spring·s</c> exactly solvable
    /// over the frame, and using that solution buys two things a naive
    /// "integrate then multiply by exp" does not: the sustained deflection is EXACTLY
    /// <c>v·k/spring</c> at any frame rate (the naive form settles a few percent low, and by a
    /// frame-rate-dependent amount), and the SUSTAINED curve is identical at 30 and 240 fps —
    /// measured to 0.002 of a stick unit across 30 / 60 / 144 / 240. The spring RATE is sampled
    /// from the radius at the START of the frame, which is the one approximation left: the radius
    /// moves a fraction of the band per frame, and the alternative (solving a nonlinear ODE per
    /// frame) would buy nothing a player could feel.</para>
    ///
    /// <para>What is deliberately NOT frame-rate independent is a single-frame flick, which
    /// lands at <c>pixels × unitsPerPixel × (1 − e) / (spring × dt)</c> — about 3% short at
    /// 60 fps and 6% at 30. That is the spring correctly acting on the deflection during the
    /// frame it was applied, not an artefact: a real flick spans several frames and settles onto
    /// the exact curve, and the alternative is a scheme whose steady state drifts with frame
    /// rate, which is the one the player would actually feel.</para>
    ///
    /// <para><b>The dead zone is applied to the OUTPUT, never to the state</b> —
    /// see <see cref="Deflection"/>. Snapping the accumulator itself is a RATCHET: a 60 fps frame
    /// whose drag adds less than the dead zone gets zeroed every frame and can never accumulate.
    /// Slow, careful mouse movement — precisely what aiming is made of — did nothing at all, and
    /// the speed it took to escape scaled with frame rate. Keeping the state honest and hiding
    /// only the report is what makes a slow drag a slow turn.</para>
    ///
    /// <para><c>holdOuter >= 1</c> disables the annulus entirely, leaving the pure spring the
    /// scheme shipped with first; <c>springPerSecond = 0</c> leaves a pure accumulator, which is
    /// what <c>DualMouseInputStrategy</c> effectively does. Both ends of the design space are one
    /// field away, not a rewrite.</para>
    /// </summary>
    public static class MouseVirtualStick
    {
        /// <summary>
        /// Below this <c>springPerSecond × deltaTime</c> the closed form's <c>(1 − e) / spring</c>
        /// loses precision against its own limit (<c>deltaTime</c>), so the no-spring branch is
        /// taken instead. It is a numerical guard, not a feel threshold — and it is also the
        /// branch the annulus takes, since the spring there is exactly 0.
        /// </summary>
        const float MinSpringStep = 1e-5f;

        /// <summary>
        /// How much of <c>springPerSecond</c> is acting at a given deflection radius: full inside
        /// <paramref name="holdInner"/>, smoothstepped away across the band, and exactly ZERO from
        /// <paramref name="holdOuter"/> out to the perimeter — the annulus the vessel holds a
        /// sustained turn in with no further mouse movement.
        ///
        /// <para><paramref name="holdOuter"/> at or above 1 means "no annulus": the spring acts
        /// everywhere, which reproduces the original model bit for bit.</para>
        /// </summary>
        public static float SpringScaleAtRadius(float radius, float holdInner, float holdOuter)
        {
            if (holdOuter >= 1f) return 1f;
            if (radius >= holdOuter) return 0f;

            // A band authored inside-out would otherwise make the two branches above disagree.
            holdInner = Mathf.Min(holdInner, holdOuter);
            if (radius <= holdInner) return 1f;

            float t = (radius - holdInner) / Mathf.Max(1e-6f, holdOuter - holdInner);
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Advance the virtual stick by one frame of mouse movement, with no annulus — the pure
        /// spring model. Kept so the original scheme stays exactly expressible and testable.
        /// </summary>
        public static Vector2 Step(Vector2 stick, Vector2 pixelDelta, float unitsPerPixel,
                                   float springPerSecond, float deltaTime)
            => Step(stick, pixelDelta, unitsPerPixel, springPerSecond, 1f, 1f, deltaTime);

        /// <summary>
        /// Advance the virtual stick by one frame of mouse movement. The result is the stick's
        /// STATE — pass it through <see cref="Deflection"/> before publishing it.
        /// </summary>
        /// <param name="stick">The stick's current state, from the previous call.</param>
        /// <param name="pixelDelta">This frame's mouse delta, in pixels.</param>
        /// <param name="unitsPerPixel">Stick units gained per pixel of movement.</param>
        /// <param name="springPerSecond">Exponential return rate toward centre, in reciprocal
        /// seconds. 0 disables the spring.</param>
        /// <param name="holdInner">Radius at which the spring starts fading out.</param>
        /// <param name="holdOuter">Radius at and beyond which the spring is dead — the inner edge
        /// of the hold annulus. 1 or more disables the annulus.</param>
        /// <param name="deltaTime">Frame time.</param>
        public static Vector2 Step(Vector2 stick, Vector2 pixelDelta, float unitsPerPixel,
                                   float springPerSecond, float holdInner, float holdOuter,
                                   float deltaTime)
        {
            float spring = springPerSecond
                         * SpringScaleAtRadius(stick.magnitude, holdInner, holdOuter);
            float springStep = spring * deltaTime;

            if (springStep > MinSpringStep)
            {
                float decay = Mathf.Exp(-springStep);
                // pixelDelta / deltaTime is this frame's drag RATE; (1 - decay) / spring is the
                // exact integral of the forcing term across the frame. Written as one factor so
                // deltaTime cancels rather than appearing twice.
                float forcing = (1f - decay) / spring;
                stick = stick * decay + pixelDelta * (unitsPerPixel * forcing / deltaTime);
            }
            else
            {
                // No spring (the annulus, a zeroed spring, or a degenerate frame time): a pure
                // accumulator, which is also the exact spring → 0 limit of the branch above.
                stick += pixelDelta * unitsPerPixel;
            }

            // Clamp to the UNIT CIRCLE, not per axis. LeftNormalizedJoystickPosition is read as a
            // RADIUS by BarrelRollController (|stick| >= perimeterThreshold arms the Sparrow's
            // strafing roll) and by ScarabJukeController, so the magnitude has to be able to
            // reach exactly 1 and must never exceed it. Clamping the STATE (not just the report)
            // is also what stops a long sweep banking deflection the player then has to unwind —
            // which matters far more here than under the pure spring, because a stick parked in
            // the annulus has nothing pulling it back off an over-swept edge.
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
        /// The deflection a sustained drag of <paramref name="pixelsPerSecond"/> settles at under
        /// the pure spring — the near-centre regime's control curve, and the thing to reason
        /// about when retuning <see cref="CosmicShore.ScriptableObjects.MouseFlightConfigSO"/>,
        /// since neither field means anything on its own. Returns 1 (pinned) when there is no
        /// spring to balance against.
        /// </summary>
        public static float SustainedDeflection(float pixelsPerSecond, float unitsPerPixel,
                                                float springPerSecond)
        {
            if (springPerSecond <= 0f) return 1f;
            return Mathf.Min(1f, pixelsPerSecond * unitsPerPixel / springPerSecond);
        }

        /// <summary>
        /// The deflection a sustained drag settles at WITH the annulus: the same curve as
        /// <see cref="SustainedDeflection"/> until the drag out-runs the spring's strongest pull,
        /// and 1 (pinned in the annulus, held with no further movement) above that.
        /// </summary>
        public static float SustainedDeflection(float pixelsPerSecond, float unitsPerPixel,
                                                float springPerSecond, float holdInner,
                                                float holdOuter)
        {
            if (pixelsPerSecond >= EscapeSpeed(unitsPerPixel, springPerSecond, holdInner, holdOuter))
                return 1f;
            return SustainedDeflection(pixelsPerSecond, unitsPerPixel, springPerSecond);
        }

        /// <summary>
        /// The drag speed at and above which the stick runs away into the annulus and stays there
        /// — the scheme's ONE commitment threshold, and the number to reason about when asking
        /// "how hard do I have to sweep to lock in a turn?".
        ///
        /// <para>It is the maximum over the whole band of the spring's restoring pull
        /// <c>spring(r) · r</c>, sampled rather than assumed. The tempting closed form is
        /// <c>spring · holdInner / k</c>, and it is WRONG — the smoothstep leaves with zero slope,
        /// so <c>spring(r) · r</c> keeps rising a little way past <paramref name="holdInner"/>
        /// before it turns over (at the shipped numbers the peak is near 0.66, about 1% above that
        /// form). A narrow band moves it further still. A threshold that silently stopped being
        /// true after a retune is the kind that gets quoted in a doc for years, so it is
        /// measured.</para>
        /// </summary>
        public static float EscapeSpeed(float unitsPerPixel, float springPerSecond,
                                        float holdInner, float holdOuter)
        {
            if (unitsPerPixel <= 0f) return float.PositiveInfinity;
            if (springPerSecond <= 0f) return 0f;          // no spring: any push commits
            if (holdOuter >= 1f)
                return springPerSecond / unitsPerPixel;    // no annulus: only the perimeter pins

            const int samples = 256;
            float peak = 0f;
            for (int i = 1; i <= samples; i++)
            {
                float r = i / (float)samples;
                peak = Mathf.Max(peak, springPerSecond * SpringScaleAtRadius(r, holdInner, holdOuter) * r);
            }
            return peak / unitsPerPixel;
        }
    }
}
