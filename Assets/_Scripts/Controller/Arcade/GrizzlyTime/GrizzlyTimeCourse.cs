using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly's circuit, stated against the GRIZZLY's own flight model - every number below
    /// is read off Grizzly.prefab, GrizzlyTriggerBombConfig.asset, VesselImpulseByExplosionEffect
    /// .asset, AOEGrizzlyExplosion.prefab and VesselTransformer rather than picked by eye, and
    /// the intensity ladder is derived from them. The solver is <see cref="HeadlongCircuit"/>,
    /// shared: what makes this course the Grizzly's is the CUT.
    ///
    /// <para><b>The Grizzly's speed is not its throttle.</b> It cruises at 50 u/s and turns at
    /// <c>50 x 0.1 + 90</c> = 95 deg/s - a 30 u circle, the tightest in the fleet. Everything
    /// past cruise comes from riding its own blasts: LT and RT each fire a bomb, a second pull
    /// freezes it, the release detonates it, and a Grizzly inside its own blast is LAUNCHED along
    /// its nose (<c>VesselImpulseByExplosionEffectSO</c>, GRIZZLY_TRIGGER_BOMBS.md). A full
    /// squeeze's blast (scale 120) hands over <c>120 / 1.2 s x 1.5</c> = 150 u/s, eased
    /// 1.5 -> 0.5 over a second and clamped by the vessel's 100 u/s velocity-modifier ceiling -
    /// so a launch sits ON the ceiling for its first ~0.7 s: 150 u/s, three times cruise. That
    /// push is a WORLD-SPACE velocity (<c>VesselTransformer.velocityShift</c>): it keeps going the
    /// way the nose pointed at detonation, and the turn rate does not see it at all.</para>
    ///
    /// <para><b>So a corner is one question: how much launch is it worth?</b> Launching into a
    /// corner keeps the 150 u/s but the hull swings round at the same 95 deg/s, a 90 u circle;
    /// coasting it lets the launch bleed off (it carries ~95 u along the heading it was aimed
    /// down) and the hull pivots on its 30 u cruise circle - and then the next bomb has to be
    /// fired, frozen and ridden before the speed is back. <see cref="CornerRadiusAtLaunch"/> is
    /// the curve, the same shape <see cref="RedlineCourse.CornerRadiusAtBoost"/> is for the
    /// Manta's Soar, at a third of the scale.</para>
    ///
    /// <para><b>The curve is the STEADY-STATE circle.</b> A launch's push points the way the nose
    /// pointed when the bomb went off, so a turn taken mid-launch slides a little wide of
    /// <see cref="FullLaunchRadius"/>. The ladder keeps every demanding corner well inside the
    /// full-launch circle, so the slide moves no corner across the line. The bomb pump this course
    /// was first cut against (2026-10-06) had the same ceiling and the same turn rate, which is
    /// why the measured ladder survived the switch to launches unchanged.</para>
    /// </summary>
    public static class GrizzlyTimeCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────
        // GrizzlyTimeCourseTests reads every one of these back off its asset.

        /// <summary>`DefaultThrottleScaler` on Grizzly.prefab - cruise at full throttle.</summary>
        public const float GrizzlyThrottleScaler = 50f;

        /// <summary>`RotationThrottleScaler` on Grizzly.prefab.</summary>
        public const float GrizzlyRotationThrottleScaler = 0.1f;

        /// <summary>The smaller of `PitchScaler` (90) and `YawScaler` (90) on Grizzly.prefab - the
        /// axis `VesselTransformer.MaxTurnRateDegreesPerSecond` reads.</summary>
        public const float GrizzlyTurnScaler = 90f;

        /// <summary>`minBlastScale` / `maxBlastScale` on GrizzlyTriggerBombConfig.asset - a tap's
        /// blast and a full squeeze's.</summary>
        public const float MinBlastScale = 30f;
        public const float MaxBlastScale = 120f;

        /// <summary>`ExplosionDuration` on AOEGrizzlyExplosion.prefab. An AOE's impulse speed is
        /// <c>MaxScale / ExplosionDuration</c> (<c>AOEExplosion</c>, Inertia 1).</summary>
        public const float ExplosionDuration = 1.2f;

        /// <summary>`selfLaunchMultiplier` on VesselImpulseByExplosionEffect.asset.</summary>
        public const float SelfLaunchMultiplier = 1.5f;

        /// <summary>`impulseDuration` on VesselImpulseByExplosionEffect.asset - seconds a launch
        /// lives (cosine ease-out).</summary>
        public const float ImpulseDuration = 1f;

        /// <summary>`velocityModifierMax` on VesselTransformer - the ceiling every launch, kick
        /// and knock-back shares.</summary>
        public const float VelocityModifierCeiling = 100f;

        /// <summary>Cruise turn rate, deg/s. A launch never changes it: `speed` is the throttle
        /// speed, and the launch rides `velocityShift`, which the turn rate does not read.</summary>
        public const float TurnRateDegPerSec =
            GrizzlyThrottleScaler * GrizzlyRotationThrottleScaler + GrizzlyTurnScaler;

        // ── The launch ───────────────────────────────────────────────────────

        /// <summary>
        /// The live weight of a launch <paramref name="age"/> seconds old - the exact factor
        /// <c>VesselTransformer.ApplyVelocityModifiers</c> applies: <c>cos(pi t / d) / 2 + 1</c>,
        /// 1.5 at birth easing to 0.5, and gone at <see cref="ImpulseDuration"/>. Its mean over
        /// the launch's life is 1.
        /// </summary>
        public static float ImpulseWeight(float age) =>
            age < 0f || age >= ImpulseDuration
                ? 0f
                : Mathf.Cos(age * Mathf.PI / ImpulseDuration) * 0.5f + 1f;

        /// <summary>The impulse a bomb of size <paramref name="size01"/> hands the Grizzly that
        /// rides it, before the ease and the ceiling: <c>blast scale / ExplosionDuration x
        /// selfLaunchMultiplier</c>. 37.5 u/s for a tap, 150 for a full squeeze.</summary>
        public static float LaunchImpulse(float size01) =>
            Mathf.Lerp(MinBlastScale, MaxBlastScale, Mathf.Clamp01(size01)) / ExplosionDuration * SelfLaunchMultiplier;

        /// <summary>Sustained speed at a share <paramref name="launch"/> in [0,1] of the
        /// velocity ceiling: cruise plus the launches riding on it. 150 u/s flat out, 50 coasting.</summary>
        public static float SpeedAtLaunch(float launch) =>
            GrizzlyThrottleScaler + VelocityModifierCeiling * Mathf.Clamp01(launch);

        /// <summary>Top speed: launching flat out. 150 u/s - the ceiling, which a full squeeze's
        /// launch reaches at birth (asserted by the tests).</summary>
        public static float TopSpeed => SpeedAtLaunch(1f);

        /// <summary>The steady circle a Grizzly flies at launch share <paramref name="launch"/>:
        /// 90 u launching flat out, 30 u coasting.</summary>
        public static float CornerRadiusAtLaunch(float launch) =>
            SpeedAtLaunch(launch) / (TurnRateDegPerSec * Mathf.Deg2Rad);

        /// <summary>THE number the course is cut around: the tightest corner a Grizzly holds
        /// without giving up any launch. ~90 u.</summary>
        public static float FullLaunchRadius => CornerRadiusAtLaunch(1f);

        /// <summary>The cruise pivot: the tightest circle the hull can fly at all. ~30 u - the
        /// floor no course may cut under.</summary>
        public static float CruiseRadius => CornerRadiusAtLaunch(0f);

        /// <summary>
        /// The most launch a Grizzly can hold through a corner of <paramref name="radius"/>, by
        /// inverting <see cref="CornerRadiusAtLaunch"/> (monotone: the turn rate is fixed, so the
        /// radius only grows with the speed). 1 at or above <see cref="FullLaunchRadius"/>.
        /// </summary>
        public static float FastestLaunchForCorner(float radius)
        {
            if (radius >= FullLaunchRadius) return 1f;
            if (radius <= CruiseRadius) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerRadiusAtLaunch(mid) > radius) hi = mid; else lo = mid;
            }
            return lo;
        }

        /// <summary>Speed the Grizzly holds through a corner of <paramref name="radius"/>.</summary>
        public static float FastestSpeedForCorner(float radius) =>
            SpeedAtLaunch(FastestLaunchForCorner(radius));

        /// <summary>
        /// How far one launch of size <paramref name="size01"/> carries the hull along the heading
        /// it was aimed down, over its whole life, clamped by the ceiling as the transformer clamps
        /// it. ~95 u for a full squeeze - about one full-launch circle, which is why a launch is
        /// aimed DOWN a leg and a corner is turned before the bomb goes off, not after.
        /// </summary>
        public static float CarryAfterLaunch(float size01 = 1f)
        {
            const float dt = 1f / 2000f;
            float impulse = LaunchImpulse(size01);
            float distance = 0f;
            for (float t = 0f; t < ImpulseDuration; t += dt)
                distance += Mathf.Min(VelocityModifierCeiling, impulse * ImpulseWeight(t)) * dt;
            return distance;
        }

        /// <summary>
        /// The shipped circuit per intensity. <b>INTENSITY IS WHAT MIX OF CORNERS A LAP ASKS
        /// FOR</b> - Headlong's and Redline's rule, cut against the Grizzly's curve. Fourteen gates
        /// a lap on a small base circle, because the hull is a third of the Manta's speed: short
        /// legs are what make a corner TIGHT on this metric (a corner's radius is half its shorter
        /// leg over the tangent of half its turn), and a leg is about one or two launches long, so
        /// every leg asks for a fresh bomb. See <see cref="GrizzlyTimeCourse"/> for the curve; the
        /// measured ladder is in GRIZZLYTIME.md §4 and asserted by GrizzlyTimeCourseTests.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float full = FullLaunchRadius;
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
                // 140 / 150+135 / 160+150+50 into one, two and three corners that cost launch.
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
        /// so fourteen gates leave 250-420 u legs - two and a half to four and a half full launches' carry
        /// (~95 u each).</summary>
        public const float BaseRadius = 560f;
    }
}
