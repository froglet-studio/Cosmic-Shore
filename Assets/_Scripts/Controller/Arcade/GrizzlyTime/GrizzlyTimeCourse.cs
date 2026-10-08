using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly's circuit, stated against the GRIZZLY's own flight model - every number below
    /// is read off Grizzly.prefab, GrizzlyTriggerBombConfig.asset, AOEGrizzlyExplosion.prefab and
    /// VesselTransformer rather than picked by eye, and
    /// the intensity ladder is derived from them. The solver is <see cref="HeadlongCircuit"/>,
    /// shared: what makes this course the Grizzly's is the CUT.
    ///
    /// <para><b>The Grizzly's speed is not its throttle.</b> It cruises at 50 u/s and turns at
    /// <c>50 x 0.1 + 90</c> = 95 deg/s - a 30 u circle, the tightest in the fleet. Everything
    /// past cruise comes from riding its own blasts: LT and RT each fire a bomb, a second pull
    /// freezes it, the release detonates it, and a Grizzly inside its own blast is LAUNCHED AWAY
    /// FROM THE BOMB (<c>GrizzlyTriggerBombExecutor.LaunchSelf</c>, GRIZZLY_TRIGGER_BOMBS.md) -
    /// so the bomb is left BEHIND you and blown. A full squeeze's blast (scale 200) hands over
    /// <c>200 / 1.2 s x 4.5</c> = 750 u/s at the bomb, eased 1.5 -> 0.5 over a second and clamped
    /// by the launch's own 300 u/s ceiling (<c>selfLaunchCeiling</c> - three times the 100 u/s
    /// every other shove shares, design ask 2026-10-08) - so a full launch sits ON that ceiling
    /// for its whole second: 350 u/s, seven times cruise. That push is a WORLD-SPACE velocity
    /// (<c>VesselTransformer.velocityShift</c>): it keeps going the way it was thrown, and the
    /// turn rate does not see it at all.</para>
    ///
    /// <para><b>So a corner is one question: how much launch is it worth?</b> Launching into a
    /// corner keeps the 350 u/s but the hull swings round at the same 95 deg/s, a 211 u circle;
    /// coasting it lets the launch bleed off (a full one carries ~300 u the way it was thrown) and
    /// the hull pivots on its 30 u cruise circle - and then the next bomb has to be
    /// fired, frozen and ridden before the speed is back. <see cref="CornerRadiusAtLaunch"/> is
    /// the curve, the same shape <see cref="RedlineCourse.CornerRadiusAtBoost"/> is for the
    /// Manta's Soar.</para>
    ///
    /// <para><b>The curve is the STEADY-STATE circle.</b> A launch's push keeps the direction it
    /// was thrown in when the bomb went off, so a turn taken mid-launch slides a little wide of
    /// <see cref="FullLaunchRadius"/>. The ladder keeps every demanding corner well inside the
    /// full-launch circle, so the slide moves no corner across the line.</para>
    ///
    /// <para><b>The cut followed the launch.</b> The course was first cut (2026-10-06) against a
    /// 100 u/s ceiling - 150 u/s top, a 90 u full-launch circle, fourteen gates on a 560 u ring.
    /// Tripling the launch (2026-10-08) made every one of those legs shorter than one launch's
    /// carry, so the lap became Headlong's octagon - eight gates on an 800 u ring - with the
    /// ladder re-measured against the 211 u circle (GRIZZLYTIME.md §4).</para>
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
        public const float MinBlastScale = 50f;
        public const float MaxBlastScale = 200f;

        /// <summary>`ExplosionDuration` on AOEGrizzlyExplosion.prefab. An AOE's impulse speed is
        /// <c>MaxScale / ExplosionDuration</c> (<c>AOEExplosion</c>, Inertia 1).</summary>
        public const float ExplosionDuration = 1.2f;

        /// <summary>`selfLaunchMultiplier` on GrizzlyTriggerBombConfig.asset.</summary>
        public const float SelfLaunchMultiplier = 4.5f;

        /// <summary>`selfLaunchSeconds` on GrizzlyTriggerBombConfig.asset - seconds a launch lives
        /// (cosine ease-out).</summary>
        public const float ImpulseDuration = 1f;

        /// <summary>`selfLaunchCeiling` on GrizzlyTriggerBombConfig.asset - the velocity ceiling a
        /// live bomb launch raises the hull's to (every other shove stays under VesselTransformer's
        /// shared 100 u/s).</summary>
        public const float VelocityModifierCeiling = 300f;

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
        /// selfLaunchMultiplier</c>. 187.5 u/s for a tap, 750 for a full squeeze, at the bomb.</summary>
        public static float LaunchImpulse(float size01) =>
            Mathf.Lerp(MinBlastScale, MaxBlastScale, Mathf.Clamp01(size01)) / ExplosionDuration * SelfLaunchMultiplier;

        /// <summary>Sustained speed at a share <paramref name="launch"/> in [0,1] of the
        /// velocity ceiling: cruise plus the launches riding on it. 350 u/s flat out, 50 coasting.</summary>
        public static float SpeedAtLaunch(float launch) =>
            GrizzlyThrottleScaler + VelocityModifierCeiling * Mathf.Clamp01(launch);

        /// <summary>Top speed: launching flat out. 350 u/s - cruise plus the ceiling, which a full
        /// squeeze's launch reaches at birth (asserted by the tests).</summary>
        public static float TopSpeed => SpeedAtLaunch(1f);

        /// <summary>The steady circle a Grizzly flies at launch share <paramref name="launch"/>:
        /// 211 u launching flat out, 30 u coasting.</summary>
        public static float CornerRadiusAtLaunch(float launch) =>
            SpeedAtLaunch(launch) / (TurnRateDegPerSec * Mathf.Deg2Rad);

        /// <summary>THE number the course is cut around: the tightest corner a Grizzly holds
        /// without giving up any launch. ~211 u.</summary>
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
        /// it, ridden at the bomb (the launch eases toward the blast's edge). ~300 u for a full
        /// squeeze - about one and a half full-launch circles, which is why a launch is thrown DOWN
        /// a leg and a corner is turned before the bomb goes off, not after.
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
        /// FOR</b> - Headlong's and Redline's rule, cut against the Grizzly's curve. Eight gates a
        /// lap on Headlong's 800 u ring: a leg is one launch's carry and a turn-in long, so every
        /// leg asks for a fresh bomb. Measured median hardest corner per level: 100% / 76% / 64% /
        /// 47% of top speed, with 0 / 1 / 2 / 3 corners costing launch. See
        /// <see cref="GrizzlyTimeCourse"/> for the curve; the measured ladder is in GRIZZLYTIME.md
        /// §4 and asserted by GrizzlyTimeCourseTests.
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
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 110f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 150f, 135f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 140f, 135f,  70f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                // The SAFETY floor in the Grizzly's own units (169 / 106 / 95 / 89 u): never
                // tighter than its 30 u cruise pivot plus a margin, so a corner the solver
                // overshot is still one the hull flies with the triggers released. Not the design
                // - CornerProfile is. CornerRadiusFactor stays 0: it would be read as a fraction of
                // the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.80f, 0.50f, 0.45f, 0.42f }[i - 1] * full,
                RadialSwing = 0.42f,
                // THE reach dial (REDLINE.md §4): the profile asks for a hairpin, but only a gap
                // squeezed hard enough produces one. Level 3 needs LESS squeeze than level 2:
                // its two big turns sit half a lap apart and pull the ring into a lens on their
                // own, where level 2's single 110 has to be squeezed to cost anything.
                AngularSpread = new[] { 1.2f, 4.0f, 2.0f, 4.0f }[i - 1],
                // Headlong's out-of-plane swing: the legs are Headlong's length now.
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                // The mouth - Headlong's (the Rhino's), because the Grizzly now arrives at a
                // Rhino's pace and, worse, on a launch it cannot steer: the push keeps the
                // direction it was thrown in, so the approach is aimed before the bomb goes off.
                RingRadius = new[] { 96f, 72f, 58f, 46f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must COVER half the level's hardest turn (a gate faces its corner's bisector,
                // so the jitter budget is `cap - halfTurn` and clamps to zero past it).
                MaxPresentDegrees = new[] { 50f, 70f, 72f, 76f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell. Eight gates
        /// leave 475-610 u legs: one full launch's carry (300 u) and a turn-in to spare on every
        /// one.</summary>
        public const float BaseRadius = 800f;
    }
}
