using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wrasse Run's cut of the shared <see cref="HeadlongCircuit"/>: Thresher Time's octagon and corner ladder, re-measured
    /// against the Grouper (the Wrasse hull) and sized so a lap is a question about the KITE, not the hull's turn.
    ///
    /// <para><b>The hull.</b> The Grouper cruises at <see cref="CruiseSpeed"/> 60 u/s (Wrasse.prefab: MinimumSpeed 10 +
    /// ThrottleScaler 50) on a 100 deg/s turn that grows 0.1 deg/s per u/s (RotationThrottleScaler), a 32 u circle. A saved
    /// boost (the Wrasse's Time loops, KiteNavigator.FireTime) multiplies the throttle by the prefab's boostMultiplier 5:
    /// <see cref="BoostSpeed"/> 260 u/s on a 118 u circle. Every corner floor sits at or above 1.6× the cruise circle, so a
    /// crew that never boosts (and every AI hull) holds every corner at full throttle.</para>
    ///
    /// <para><b>The kite.</b> A ring counts only when the kite threads it, and the kite lives on a 160 u board in the
    /// hull's plane, its centre 48 u ahead. So the pilot does not need to thread the mouth; the pilot needs the ring IN the
    /// plane and ON the board. <see cref="HeadlongCircuitSettings.LateralPerturbation"/> is the dial for that: it lifts
    /// rings out of the circuit's plane, so a higher level asks the Grouper to roll and pitch the board onto each one.
    /// The mouths are a kite's (its head is 8 u), not a hull's.</para>
    ///
    /// <para><c>WrasseRunCourseTests</c> reads the hull constants back off Wrasse.prefab.</para>
    /// </summary>
    public static class WrasseRunCourse
    {
        // ── The vessel this course is cut for (read back off Wrasse.prefab by the tests) ─────────
        public const float WrasseThrottleScaler = 50f;
        public const float WrasseMinimumSpeed = 10f;
        public const float WrasseTurnScaler = 100f;
        public const float WrasseRotationThrottleScaler = 0.1f;
        public const float WrasseBoostMultiplier = 5f;

        // ── The kite (KiteNavigatorConfig.asset) ──────────────────────────────────────────────────
        public const float BoardRadius = 160f;

        // ── The speeds ────────────────────────────────────────────────────────────────────────────
        public const float CruiseSpeed = WrasseMinimumSpeed + WrasseThrottleScaler;
        public const float BoostSpeed = WrasseMinimumSpeed + WrasseThrottleScaler * WrasseBoostMultiplier;

        public static float TurnRateDegPerSec(float speed) => speed * WrasseRotationThrottleScaler + WrasseTurnScaler;

        public static float CircleAt(float speed) => speed / (TurnRateDegPerSec(speed) * Mathf.Deg2Rad);

        /// <summary>The cruise circle (~32 u).</summary>
        public static float CruiseRadius => CircleAt(CruiseSpeed);

        /// <summary>The boosted circle (~118 u): the reference the corners are cut against.</summary>
        public static float BoostRadius => CircleAt(BoostSpeed);

        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle (u), inside the barren race cell's 480-1080 u shell.</summary>
        public const float BaseRadius = 640f;

        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new HeadlongCircuitSettings
            {
                GateCount = GatesPerLap,
                BaseRadius = BaseRadius,
                // Thresher Time's profiles (Grizzly Time's): 0 / 1 / 2 / 3 corners that cost speed.
                CornerProfile = new[]
                {
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 110f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 150f, 135f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 140f, 135f,  70f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                // The SAFETY floor in the Wrasse's own units: 0.80 / 0.60 / 0.50 / 0.45 x the 118 u boosted circle
                // (94 / 71 / 59 / 53 u), every one at least 1.6x the 32 u cruise circle.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.80f, 0.60f, 0.50f, 0.45f }[i - 1] * BoostRadius,
                RadialSwing = 0.42f,
                AngularSpread = new[] { 1.2f, 4.0f, 2.0f, 4.0f }[i - 1],
                // Rings lifted out of the circuit's plane: the pilot's job is to roll the kite's board onto each one.
                LateralPerturbation = new[] { 60f, 100f, 140f, 180f }[i - 1],
                // A kite's mouths: its head is 8 u, and it is slung, not steered.
                RingRadius = new[] { 60f, 50f, 42f, 36f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 70f, 72f, 76f }[i - 1],
            };
        }
    }
}
