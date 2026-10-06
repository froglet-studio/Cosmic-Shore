using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly's circuit, stated against the GRIZZLY's own flight model - every number below
    /// is read off Grizzly.prefab, GrizzlyBombPumpConfig.asset and VesselTransformer rather than
    /// picked by eye, and the intensity ladder is derived from them. The solver is
    /// <see cref="HeadlongCircuit"/>, shared: what makes this course the Grizzly's is the CUT.
    ///
    /// <para><b>The Grizzly's speed is not its throttle.</b> It cruises at 50 u/s and turns at
    /// <c>50 x 0.1 + 90</c> = 95 deg/s - a 30 u circle, the tightest in the fleet. Everything
    /// past cruise comes from the BOMB PUMP: each trigger release blows a bomb that adds up to
    /// 40 u/s along the nose for 0.8 s, each trigger on its own 0.45 s clock, so alternating
    /// LT/RT lands a kick every 0.225 s and the stacked kicks sit on the vessel's 100 u/s
    /// velocity-modifier ceiling - 150 u/s, three times cruise. Those kicks are a WORLD-SPACE
    /// velocity (<c>VesselTransformer.velocityShift</c>): they keep going the way the nose
    /// pointed when they were blown, and the turn rate does not see them at all.</para>
    ///
    /// <para><b>So a corner is one question: how much pump is it worth?</b> Pumping through a
    /// corner keeps the 150 u/s but the momentum swings round at the same 95 deg/s, a 90 u
    /// circle; lifting off the triggers lets the stacked kicks bleed off (a slide of up to ~53 u
    /// along the old heading) and the hull pivots on its 30 u cruise circle - and then the pump has to be
    /// wound back up, three bombs to the ceiling. <see cref="CornerRadiusAtPump"/> is the curve,
    /// the same shape <see cref="RedlineCourse.CornerRadiusAtBoost"/> is for the Manta's Soar,
    /// at a third of the scale: the Grizzly races the same circuit solver on a course cut tight
    /// and short.</para>
    ///
    /// <para><b>The curve is the STEADY-STATE circle.</b> In a sustained turn the live kicks
    /// point along the last 0.8 s of nose headings, so their sum is shorter than a straight
    /// line's and the true pumped path lags a little wide of <see cref="FullPumpRadius"/>. The
    /// ladder is cut against the steady circle and keeps every demanding corner well inside it,
    /// so the lag moves no corner across the line.</para>
    /// </summary>
    public static class GrizzlyTimeCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────
        // Read from Grizzly.prefab (VesselTransformer), GrizzlyBombPumpConfig.asset and
        // VesselTransformer.velocityModifierMax. GrizzlyTimeCourseTests pins every one.

        /// <summary>`DefaultThrottleScaler` on Grizzly.prefab - cruise at full throttle.</summary>
        public const float GrizzlyThrottleScaler = 50f;

        /// <summary>`RotationThrottleScaler` on Grizzly.prefab.</summary>
        public const float GrizzlyRotationThrottleScaler = 0.1f;

        /// <summary>The smaller of `PitchScaler` (90) and `YawScaler` (90) on Grizzly.prefab - the
        /// axis `VesselTransformer.MaxTurnRateDegreesPerSecond` reads.</summary>
        public const float GrizzlyTurnScaler = 90f;

        /// <summary>`maxKick` on GrizzlyBombPumpConfig.asset - a full-pressure bomb's kick, u/s.</summary>
        public const float PumpMaxKick = 40f;

        /// <summary>`kickDuration` on GrizzlyBombPumpConfig.asset - seconds each kick lives.</summary>
        public const float PumpKickDuration = 0.8f;

        /// <summary>`cooldownPerTrigger` on GrizzlyBombPumpConfig.asset. Two triggers, two clocks:
        /// alternating lands a bomb every half of this.</summary>
        public const float PumpCooldownPerTrigger = 0.45f;

        /// <summary>`velocityModifierMax` on VesselTransformer - the ceiling every stacked kick
        /// shares.</summary>
        public const float VelocityModifierCeiling = 100f;

        /// <summary>Cruise turn rate, deg/s. The pump never changes it: `speed` is the throttle
        /// speed, and the kicks ride `velocityShift`, which the turn rate does not read.</summary>
        public const float TurnRateDegPerSec =
            GrizzlyThrottleScaler * GrizzlyRotationThrottleScaler + GrizzlyTurnScaler;

        // ── The pump ─────────────────────────────────────────────────────────

        /// <summary>
        /// The live weight of a kick <paramref name="age"/> seconds old - the exact factor
        /// <c>VesselTransformer.ApplyVelocityModifiers</c> applies: <c>cos(pi t / d) / 2 + 1</c>,
        /// 1.5 at birth easing to 0.5, and gone at <see cref="PumpKickDuration"/>. Its mean over
        /// the kick's life is 1.
        /// </summary>
        public static float KickWeight(float age) =>
            age < 0f || age >= PumpKickDuration
                ? 0f
                : Mathf.Cos(age * Mathf.PI / PumpKickDuration) * 0.5f + 1f;

        /// <summary>
        /// The raw (unclamped) surplus of a straight-line pump at bomb size <paramref name="pump"/>
        /// in [0,1], time-averaged over a steady LT/RT rhythm: kicks of <c>pump x maxKick</c>
        /// every half a trigger cooldown, each worth its mean weight 1 for its whole life.
        /// ~142 u/s at a full squeeze - which is why the ceiling, not the bomb, sets top speed.
        /// </summary>
        public static float RawPumpSurplus(float pump) =>
            Mathf.Clamp01(pump) * PumpMaxKick * PumpKickDuration / (PumpCooldownPerTrigger * 0.5f);

        /// <summary>Sustained speed at bomb size <paramref name="pump"/>: cruise plus the stacked
        /// kicks, clamped by the velocity-modifier ceiling. 150 u/s flat out, 50 at rest.</summary>
        public static float SpeedAtPump(float pump) =>
            GrizzlyThrottleScaler + Mathf.Min(VelocityModifierCeiling, RawPumpSurplus(pump));

        /// <summary>Top speed: every bomb a full squeeze, triggers alternating. 150 u/s.</summary>
        public static float TopSpeed => SpeedAtPump(1f);

        /// <summary>The steady circle a Grizzly flies at bomb size <paramref name="pump"/>: 90 u
        /// pumping flat out, 30 u on the throttle alone.</summary>
        public static float CornerRadiusAtPump(float pump) =>
            SpeedAtPump(pump) / (TurnRateDegPerSec * Mathf.Deg2Rad);

        /// <summary>THE number the course is cut around: the tightest corner a Grizzly holds
        /// without lifting off the pump. ~90 u.</summary>
        public static float FullPumpRadius => CornerRadiusAtPump(1f);

        /// <summary>The cruise pivot: the tightest circle the hull can fly at all, triggers
        /// released. ~30 u - the floor no course may cut under.</summary>
        public static float CruiseRadius => CornerRadiusAtPump(0f);

        /// <summary>
        /// The most pump a Grizzly can hold through a corner of <paramref name="radius"/>, by
        /// inverting <see cref="CornerRadiusAtPump"/> (monotone: the turn rate is fixed, so the
        /// radius only grows with the speed). 1 at or above <see cref="FullPumpRadius"/>.
        /// </summary>
        public static float FastestPumpForCorner(float radius)
        {
            if (radius >= FullPumpRadius) return 1f;
            if (radius <= CruiseRadius) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerRadiusAtPump(mid) > radius) hi = mid; else lo = mid;
            }
            return lo;
        }

        /// <summary>Speed the Grizzly holds through a corner of <paramref name="radius"/>.</summary>
        public static float FastestSpeedForCorner(float radius) =>
            SpeedAtPump(FastestPumpForCorner(radius));

        /// <summary>
        /// How far the stacked kicks carry the hull along its OLD heading once the pump stops -
        /// what a pilot who lifts at a corner slides before the pivot bites. Integrated over the
        /// live kicks of a steady full-pressure pump at the instant of its last bomb (the worst
        /// case), clamped by the ceiling as the transformer clamps it. ~53 u: nearly two cruise
        /// circles of overshoot, which is why a lift has to come a beat BEFORE the gate.
        /// </summary>
        public static float SlideAfterLift()
        {
            const float dt = 1f / 2000f;
            float interval = PumpCooldownPerTrigger * 0.5f;
            int live = Mathf.CeilToInt(PumpKickDuration / interval) + 1;
            float distance = 0f;
            for (float t = 0f; t < PumpKickDuration; t += dt)
            {
                float sum = 0f;
                // The last bomb at age 0, the one before it at age `interval`, ...
                for (int k = 0; k < live; k++)
                    sum += PumpMaxKick * KickWeight(t + k * interval);
                distance += Mathf.Min(VelocityModifierCeiling, sum) * dt;
            }
            return distance;
        }

        /// <summary>
        /// The shipped circuit per intensity. <b>INTENSITY IS WHAT MIX OF CORNERS A LAP ASKS
        /// FOR</b> - Headlong's and Redline's rule, cut against the Grizzly's curve. Fourteen gates
        /// a lap on a small base circle, because the hull is a third of the Manta's speed: short
        /// legs are what make a corner TIGHT on this metric (a corner's radius is half its shorter
        /// leg over the tangent of half its turn), and short legs are what make the pump a rhythm
        /// rather than a held button. See <see cref="GrizzlyTimeCourse"/> for the curve; the
        /// measured ladder is in GRIZZLYTIME.md §4 and asserted by GrizzlyTimeCourseTests.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float full = FullPumpRadius;
            return new HeadlongCircuitSettings
            {
                GateCount = GatesPerLap,
                BaseRadius = BaseRadius,
                CornerProfile = new[]
                {
                    new[] {  40f,  36f,  34f,  32f,  30f,  28f,  26f,  24f,  22f,  20f,  20f,  18f,  16f,  14f },
                    new[] { 140f,  40f,  30f,  26f,  22f,  20f,  18f,  16f,  14f,  10f,   8f,   8f,   4f,   4f },
                    new[] { 150f, 135f,  20f,  15f,  12f,   8f,   6f,   5f,   4f,   3f,   2f,   0f,   0f,   0f },
                    new[] { 160f, 150f,  50f,   0f,   0f,   0f,   0f,   0f,   0f,   0f,   0f,   0f,   0f,   0f },
                }[i - 1],
                // The SAFETY floor in the Grizzly's own units (72 / 45 / 41 / 38 u): never tighter
                // than its 30 u cruise pivot plus a margin, so a corner the solver overshot is
                // still one the hull flies with the triggers released. Not the design -
                // CornerProfile is. CornerRadiusFactor stays 0: it would be read as a fraction of
                // the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.80f, 0.50f, 0.45f, 0.42f }[i - 1] * full,
                // Measured inert on this cut (the flat ring already sits against the nucleus
                // shell); kept at Headlong's and Redline's value so the three cuts read alike.
                RadialSwing = 0.42f,
                // THE reach dial (REDLINE.md §4): the profile asks for a hairpin, but only a gap
                // squeezed hard enough produces one. 3 / 6 / 10 are what turn levels 2-4's asked-for
                // 140 / 150+135 / 160+150+50 into one, two and three corners that cost pump.
                AngularSpread = new[] { 1.2f, 3.0f, 6.0f, 10.0f }[i - 1],
                // Less out-of-plane than Redline (160-280): the legs are a third as long, and a
                // swing that size would turn every leg into a corner of its own.
                LateralPerturbation = new[] { 80f, 110f, 140f, 170f }[i - 1],
                // The mouth. The Grizzly arrives at a third of the Manta's speed with three times
                // its turn rate, so it can be asked to thread far tighter rings (Redline: 110 / 88
                // / 72 / 44). Level 4's 34 u is a hull-and-a-bit for a pilot carrying a slide.
                RingRadius = new[] { 64f, 52f, 42f, 34f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must COVER half the level's hardest turn (a gate faces its corner's bisector,
                // so the jitter budget is `cap - halfTurn` and clamps to zero past it).
                MaxPresentDegrees = new[] { 50f, 82f, 84f, 86f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 14;

        /// <summary>The circuit's base circle. Close outside the race cell's nucleus shell (480),
        /// so fourteen gates leave 250-420 u legs - a second and a half to three seconds of pump
        /// each at 150 u/s.</summary>
        public const float BaseRadius = 560f;
    }
}
