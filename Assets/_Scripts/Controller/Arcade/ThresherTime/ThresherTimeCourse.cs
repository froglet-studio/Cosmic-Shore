using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's circuit, stated against the THRESHER's own flight model - every number below
    /// is read off Thresher.prefab, ThresherConfig.asset and VesselTransformer rather than picked
    /// by eye, and the intensity ladder is derived from them. The solver is
    /// <see cref="HeadlongCircuit"/>, shared: what makes this course the Thresher's is the CUT.
    ///
    /// <para><b>The Thresher's speed is not its throttle.</b> It cruises at <c>15 + 110</c> =
    /// 125 u/s at full throttle and turns at a flat 120 deg/s (`RotationThrottleScaler` 0, so the
    /// rate never grows with speed) - a 60 u circle. Everything past cruise comes from SLINGING
    /// (THRESHER.md §4-§4a, §5a): a full LT pull stops the ball and plants it, the ship hooks onto
    /// an orbit round it, the orbit spins up, and RT - the chain's rigidity - winds it in, which
    /// spins it up further by angular momentum (<c>ThresherChainSolver.StepElasticRadius</c>: the
    /// orbit's speed scales by old radius / new radius). Letting go throws the hull off the circle
    /// at the orbit's speed. The orbit's speed is clamped by the BALL's ceiling,
    /// <c>maxBall x gameCruise / sandboxCruise</c> = 479 u/s, so that is the sling's top speed.
    /// Measured with the shipped solver at ThresherConfig's analog dials (THRESHERTIME.md §1): a
    /// rigid plant held 1 s leaves at ~230 u/s, a slack orbit held 2 s then wound in leaves at
    /// ~360, held 3 s at ~450.</para>
    ///
    /// <para><b>A sling is the hull's OWN momentum, not a push.</b> The Thresher flies the vector
    /// flight model, whose grip snaps momentum onto the nose every frame outside a drift - so,
    /// unlike the Grizzly's world-space blast launch, the hull turns ALL of a sling at its 120
    /// deg/s, on a circle that grows with the speed: 229 u at the ceiling, 60 u at cruise. And the
    /// throttle bleeds it back to cruise at <c>LERP_AMOUNT</c> 1.5/s (the towed ball's tug a
    /// little faster). <see cref="CornerRadiusAtSling"/> is the curve, the same shape
    /// <see cref="GrizzlyTimeCourse.CornerRadiusAtLaunch"/> is for the Grizzly.</para>
    ///
    /// <para><b>So a corner is one question: how much sling is it worth?</b> A corner wider than
    /// the full-sling circle keeps a ceiling launch; a tighter one throws it wide, and the pilot
    /// either lets the sling bleed before the turn-in or turns the corner by planting at it - the
    /// plant is itself a corner, any angle round the ball, and the next sling. The cut's
    /// reference circle (229 u) is within 8% of the Grizzly's full-launch circle (211 u), so the
    /// Grizzly Time corner profiles produce the same ladder here; the floors, the curve and the
    /// leg rule are the Thresher's.</para>
    ///
    /// <para><b>The curve is the STEADY-STATE circle at the speed held</b>, and a sling's speed is
    /// not held: it bleeds (<see cref="LaunchSpeedAfter"/>). A corner taken a second after the
    /// release is taken on a smaller circle than the release speed's. The ladder states what a
    /// corner COSTS a launch carried into it, which is the decision the pilot makes.</para>
    /// </summary>
    public static class ThresherTimeCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────
        // ThresherTimeCourseTests reads every one of these back off its asset (or source file).

        /// <summary>`DefaultThrottleScaler` on Thresher.prefab (ThresherVesselTransformer) - the
        /// throttle's span.</summary>
        public const float ThresherThrottleScaler = 110f;

        /// <summary>`DefaultMinimumSpeed` on Thresher.prefab - the floor under the throttle.</summary>
        public const float ThresherMinimumSpeed = 15f;

        /// <summary>`RotationThrottleScaler` on Thresher.prefab. 0: the turn rate does not grow
        /// with speed, so the circle grows in proportion to it.</summary>
        public const float ThresherRotationThrottleScaler = 0f;

        /// <summary>The smaller of `PitchScaler` (120) and `YawScaler` (120) on Thresher.prefab -
        /// the axis `VesselTransformer.MaxTurnRateDegreesPerSecond` reads.</summary>
        public const float ThresherTurnScaler = 120f;

        /// <summary>`dials.sandboxCruise` on ThresherConfig.asset - the 2D sandbox's cruise, which
        /// every chain dial is authored in (THRESHER.md §4).</summary>
        public const float SandboxCruise = 380f;

        /// <summary>`dials.gameCruise` on ThresherConfig.asset - the game cruise the dials are
        /// scaled to (k = gameCruise / sandboxCruise).</summary>
        public const float GameCruise = 70f;

        /// <summary>`dials.maxBall` on ThresherConfig.asset, in SANDBOX units - the ball's speed
        /// cap, which also clamps the planted orbit's speed (`ThresherChainSolver` clamps
        /// `LockSpeed` to `MaxBallSpeed`), so it is the sling's ceiling.</summary>
        public const float MaxBallSandbox = 2600f;

        /// <summary>`dials.lockMax` on ThresherConfig.asset - where the orbit's own spin-up stops,
        /// as a multiple of `gameCruise` (154 u/s). Only the wind-in carries an orbit past it.</summary>
        public const float LockMax = 2.2f;

        /// <summary>`launchBoost` on ThresherConfig.asset - EVERY aimed launch (out of the crosshair
        /// view) leaves this many times the orbit's speed, whatever its timing. Until 2026-10-13 this
        /// was Time L5's x1.6 "Perfect Release" on a timing window.</summary>
        public const float LaunchBoost = 4.8f;

        /// <summary>`LERP_AMOUNT` in VesselTransformer.cs (a C# constant, not an asset): the rate
        /// at which the throttle's step pulls the hull's speed toward its target, so the rate a
        /// sling bleeds back to cruise.</summary>
        public const float ThrottleTrackingRate = 1.5f;

        // ── The speeds ───────────────────────────────────────────────────────

        /// <summary>Cruise at full throttle: <c>DefaultMinimumSpeed + DefaultThrottleScaler</c>,
        /// 125 u/s. (Neutral sticks fly at <see cref="GameCruise"/>, 70.)</summary>
        public const float CruiseSpeed = ThresherMinimumSpeed + ThresherThrottleScaler;

        /// <summary>The sling's ceiling, in game units: <c>maxBall x gameCruise / sandboxCruise</c>,
        /// ~479 u/s - the fastest an orbit may spin, so the fastest a release leaves.</summary>
        public const float SlingCeiling = MaxBallSandbox * GameCruise / SandboxCruise;

        /// <summary>Where the orbit's own spin-up stops: <c>lockMax x gameCruise</c>, 154 u/s at
        /// Time at rest. The wind-in carries the orbit past it, up to <see cref="SlingCeiling"/>.</summary>
        public const float OrbitSpinCap = LockMax * GameCruise;

        /// <summary>The fastest a Thresher can ever leave a sling: the ceiling out of the crosshair
        /// view, boosted (~2299 u/s). Every human's aimed launch takes the boost, so this is reachable
        /// by anyone who winds a sling to the ceiling - the ladder is still cut against the UNBOOSTED
        /// ceiling (THRESHERTIME.md §3, "the boost outruns the ladder"). The number the scene's
        /// detection clamp has to cover.</summary>
        public const float BoostedSlingSpeed = SlingCeiling * LaunchBoost;

        /// <summary>Turn rate at <paramref name="speed"/>, deg/s - the transformer's own formula,
        /// <c>speed x RotationThrottleScaler + min(Pitch, Yaw)</c>. A flat 120 for the Thresher.</summary>
        public static float TurnRateDegPerSec(float speed) =>
            speed * ThresherRotationThrottleScaler + ThresherTurnScaler;

        // ── The sling ────────────────────────────────────────────────────────

        /// <summary>Speed carried at a share <paramref name="sling"/> in [0,1] of the sling's span
        /// above cruise: 479 u/s at a full (ceiling) sling, 125 cruising.</summary>
        public static float SpeedAtSling(float sling) =>
            CruiseSpeed + (SlingCeiling - CruiseSpeed) * Mathf.Clamp01(sling);

        /// <summary>Top speed: a sling at the ceiling. ~479 u/s.</summary>
        public static float TopSpeed => SpeedAtSling(1f);

        /// <summary>The steady circle a Thresher flies at sling share <paramref name="sling"/>:
        /// ~229 u at the ceiling, ~60 u cruising. Speed over turn rate - grip keeps a sling on the
        /// nose, so the whole of it turns at the hull's rate.</summary>
        public static float CornerRadiusAtSling(float sling)
        {
            float v = SpeedAtSling(sling);
            return v / (TurnRateDegPerSec(v) * Mathf.Deg2Rad);
        }

        /// <summary>THE number the course is cut around: the tightest corner a Thresher holds
        /// without giving up any of a ceiling sling. ~229 u.</summary>
        public static float FullSlingRadius => CornerRadiusAtSling(1f);

        /// <summary>The cruise circle at full throttle - ~60 u. Every floor sits well above it, so
        /// a pilot who never slings (and every AI, which cannot) holds every corner at cruise.</summary>
        public static float CruiseRadius => CornerRadiusAtSling(0f);

        /// <summary>
        /// The most sling a Thresher can carry through a corner of <paramref name="radius"/>, by
        /// inverting <see cref="CornerRadiusAtSling"/> (monotone: speed over a turn rate that does
        /// not shrink with speed). 1 at or above <see cref="FullSlingRadius"/>.
        /// </summary>
        public static float FastestSlingForCorner(float radius)
        {
            if (radius >= FullSlingRadius) return 1f;
            if (radius <= CruiseRadius) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerRadiusAtSling(mid) > radius) hi = mid; else lo = mid;
            }
            return lo;
        }

        /// <summary>Speed the Thresher carries through a corner of <paramref name="radius"/>.</summary>
        public static float FastestSpeedForCorner(float radius) =>
            SpeedAtSling(FastestSlingForCorner(radius));

        /// <summary>
        /// A sling's speed <paramref name="seconds"/> after it left at <paramref name="releaseSpeed"/>:
        /// the throttle's step pulls the hull back to cruise exponentially, at
        /// <see cref="ThrottleTrackingRate"/>. The towed ball's tug makes the real bleed a little
        /// faster (measured: a 452 u/s release is at ~192 u/s a second later; this says ~198).
        /// </summary>
        public static float LaunchSpeedAfter(float releaseSpeed, float seconds) =>
            CruiseSpeed + (releaseSpeed - CruiseSpeed) * Mathf.Exp(-ThrottleTrackingRate * Mathf.Max(0f, seconds));

        /// <summary>
        /// How far a sling released at <paramref name="releaseSpeed"/> carries the hull while it
        /// bleeds <paramref name="bledShare"/> (0..1) of its excess over cruise - the integral of
        /// <see cref="LaunchSpeedAfter"/>. ~404 u for a ceiling sling to bleed 90%: the length a leg
        /// needs for a release at the ceiling to be SPENT on it rather than thrown into the next
        /// corner.
        /// </summary>
        public static float BleedDistance(float releaseSpeed, float bledShare)
        {
            float share = Mathf.Clamp(bledShare, 0f, 0.999f);
            float t = -Mathf.Log(1f - share) / ThrottleTrackingRate;
            return CruiseSpeed * t + (releaseSpeed - CruiseSpeed) * share / ThrottleTrackingRate;
        }

        /// <summary>
        /// The shipped circuit per intensity. <b>INTENSITY IS WHAT MIX OF CORNERS A LAP ASKS
        /// FOR</b> - Headlong's, Redline's and Grizzly Time's rule, cut against the Thresher's
        /// sling curve. Eight gates a lap on Headlong's 800 u ring: every leg holds a ceiling
        /// sling's bleed, so every leg is somewhere to spend one. Measured median hardest corner
        /// per level: 100% / 71% / 59% / 44% of top speed, with 0 / 1 / 2 / 3 corners costing
        /// sling. See <see cref="ThresherTimeCourse"/> for the curve; the measured ladder is in
        /// THRESHERTIME.md §3 and asserted by ThresherTimeCourseTests.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float full = FullSlingRadius;
            return new HeadlongCircuitSettings
            {
                GateCount = GatesPerLap,
                BaseRadius = BaseRadius,
                // Grizzly Time's profiles: the two hulls' reference circles are within 8% of each
                // other (229 u here, 211 u there), and measured against the Thresher's curve they
                // give the 0 / 1 / 2 / 3 ladder unchanged.
                CornerProfile = new[]
                {
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 110f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 150f, 135f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 140f, 135f,  70f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                // The SAFETY floor in the Thresher's own units (183 / 114 / 103 / 96 u): at least
                // 1.6x its 60 u cruise circle at every level, so a corner the solver overshot is
                // still one a Thresher that never slings - every AI - flies at full throttle. Not
                // the design - CornerProfile is. CornerRadiusFactor stays 0: it would be read as a
                // fraction of the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.80f, 0.50f, 0.45f, 0.42f }[i - 1] * full,
                RadialSwing = 0.42f,
                // THE reach dial (REDLINE.md §4), Grizzly Time's spreads: level 2's single 110
                // has to be squeezed hard to cost anything; level 3's two big turns sit half a
                // lap apart and pull the ring into a lens on their own.
                AngularSpread = new[] { 1.2f, 4.0f, 2.0f, 4.0f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                // The mouth - Headlong's (the Rhino's): a sling arrives at up to 479 u/s, a
                // Rhino's pace, on a hull that corrects at only 120 deg/s - a 229 u circle at
                // the ceiling, so its lateral authority on the way in is a Rhino's too.
                RingRadius = new[] { 96f, 72f, 58f, 46f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must COVER half the level's hardest turn (a gate faces its corner's bisector,
                // so the jitter budget is `cap - halfTurn` and clamps to zero past it). Measured
                // worst half-turns: 43.5 / 67.2 / 68.2 / 73.3.
                MaxPresentDegrees = new[] { 50f, 70f, 72f, 76f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell. Eight gates
        /// leave 470-610 u legs at the median: every one longer than a ceiling sling's 90% bleed
        /// (~404 u), so a sling released on a leg is spent on it.</summary>
        public const float BaseRadius = 800f;
    }
}
