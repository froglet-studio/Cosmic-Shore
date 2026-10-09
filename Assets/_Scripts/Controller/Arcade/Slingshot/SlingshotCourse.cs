using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's circuit (<c>Arcade/SLINGSHOT.md</c>), stated against the STOAT's own flight
    /// model and its wormhole sling - every constant below is read off Stoat.prefab and the
    /// shipped configs, and <c>SlingshotCourseTests</c> reads them back. The solver is
    /// <see cref="HeadlongCircuit"/>, shared: what makes this course the Stoat's is the CUT.
    ///
    /// <para><b>The vessel.</b> The Stoat flies the Squirrel's transformer: 60 u/s at full
    /// throttle, a flat 120°/s turn (<c>RotationThrottleScaler</c> 0), so it pivots on a 29 u
    /// circle. In the barren race cell there is nothing to skim, so the only speed past cruise is
    /// the WORMHOLE PULL of its own slung pair - <c>BlackHoleVesselPull</c>, clamped to
    /// <c>BlackHoleConfig.maxVesselPullSpeed</c> (90 u/s). Slung flat out it makes 150 u/s on a
    /// 72 u circle.</para>
    ///
    /// <para><b>What a corner asks.</b> At this scale no corner the solver lays is tighter than
    /// the slung circle, so a corner never costs the Stoat speed the way it costs a Manta its
    /// Soar. The question is the THROW: the pull drags the hull toward the attractor, off the
    /// line it was flying, so a sling is a decision about where the pull will have put you when
    /// the next ring arrives. Intensity sharpens that: tighter, more out-of-plane turns (the
    /// sling has to turn you as well as push you) and smaller mouths (the throw has to be
    /// aimed). Measured, not reasoned: <c>Tools/Build/slingshot_course_harness</c> runs the
    /// shipped solver over 400 seeds per level and prints the corners and legs.</para>
    /// </summary>
    public static class SlingshotCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────

        /// <summary>`DefaultThrottleScaler` on Stoat.prefab - cruise speed at full throttle.</summary>
        public const float StoatThrottleScaler = 60f;

        /// <summary>`YawScaler` / `PitchScaler` on Stoat.prefab (both 120): the turn rate, flat,
        /// because `RotationThrottleScaler` is 0.</summary>
        public const float StoatTurnScaler = 120f;

        /// <summary>`BlackHoleConfig.maxVesselPullSpeed` - the most a slung pair adds.</summary>
        public const float SlingPullCeiling = 90f;

        /// <summary>Speed with the pull at <paramref name="sling01"/> of its ceiling.</summary>
        public static float SpeedAtSling(float sling01) => StoatThrottleScaler + SlingPullCeiling * Mathf.Clamp01(sling01);

        /// <summary>The circle the Stoat holds at that speed.</summary>
        public static float CornerRadiusAtSling(float sling01) => SpeedAtSling(sling01) / (StoatTurnScaler * Mathf.Deg2Rad);

        /// <summary>~29 u: the cruise pivot.</summary>
        public static float CruiseRadius => CornerRadiusAtSling(0f);

        /// <summary>~72 u: slung flat out.</summary>
        public static float FullSlingRadius => CornerRadiusAtSling(1f);

        // ── The circuit ──────────────────────────────────────────────────────

        public const int GatesPerLap = 8;

        /// <summary>The base circle. The race shell is 480..1080 (the barren cell's nucleus x 1.22
        /// to its membrane), so 600 is as small as the circuit can sit and still swing: a regular
        /// octagon there has ~460 u legs, about 7.7 s at cruise and 3 s slung.</summary>
        public const float BaseRadius = 600f;

        /// <summary>
        /// The shipped circuit per intensity. <b>Intensity is how hard the throw has to be aimed</b>:
        /// the turns sharpen and leave the plane, and the mouths close in on the Stoat's ~8 u body.
        /// The corner profiles are Grizzly Time's (the other slow hull whose speed is something it
        /// lays down and rides), and the safety floor never goes below twice the cruise pivot, so a
        /// corner the solver overshot is still one a Stoat makes without slinging at all.
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = GatesPerLap,
                BaseRadius = BaseRadius,
                CornerProfile = new[]
                {
                    new[] {  90f,  72f,  58f,  48f,  38f,  28f,  16f,  10f },
                    new[] { 110f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 150f, 135f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 140f, 135f,  70f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                // ABSOLUTE floor in the Stoat's units: 2 x / 1.6 x / 1.3 x / 1.2 x the slung circle
                // (143 / 115 / 93 / 86 u), never below 2 x the cruise pivot. CornerRadiusFactor stays
                // 0 - it would be read as a fraction of the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = Mathf.Max(2f * CruiseRadius, new[] { 2.0f, 1.6f, 1.3f, 1.2f }[i - 1] * FullSlingRadius),
                RadialSwing = 0.42f,
                AngularSpread = new[] { 1.2f, 3.0f, 2.0f, 4.0f }[i - 1],
                // Out of plane: a sling is laid on the hull's own horizontal, so a turn that climbs
                // asks the pilot to ROLL the pair into it - the skill the upper levels ask for.
                LateralPerturbation = new[] { 80f, 120f, 160f, 200f }[i - 1],
                // The mouth, sized to an 8 u stoat arriving at 60-150 u/s on a throw it cannot fully
                // steer: generous at 1, a precise throw at 4.
                RingRadius = new[] { 48f, 40f, 32f, 26f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must cover half the level's hardest turn (a gate faces its corner's bisector).
                MaxPresentDegrees = new[] { 50f, 60f, 76f, 76f }[i - 1],
            };
        }
    }
}
