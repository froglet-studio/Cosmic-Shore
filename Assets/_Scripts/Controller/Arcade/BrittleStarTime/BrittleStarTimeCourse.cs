using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's circuit, stated against its flight model (BrittleStar.prefab and
    /// BrittleStarConfig.asset; BrittleStarTimeCourseTests reads them back). The solver is
    /// <see cref="HeadlongCircuit"/>, shared: what makes this course the Brittle Star's is the CUT.
    ///
    /// <para><b>Speed is a mode.</b> Cruise is <c>(10 + 35)</c> = 45 u/s; Speed Mode multiplies the
    /// throttle target by <see cref="SpeedModeMultiplier"/> (6), 270 u/s. Turn rate is
    /// <c>0.1 x speed + 80</c> deg/s, so Speed Mode turns a ~145 u circle. The floors sit around that
    /// circle: at I1 every corner holds Speed Mode, and each level adds corners that do not.</para>
    /// </summary>
    public static class BrittleStarTimeCourse
    {
        // ── The vessel this course is cut for (BrittleStarTimeCourseTests reads each back off its asset) ──

        /// <summary>`DefaultMinimumSpeed` on BrittleStar.prefab.</summary>
        public const float MinimumSpeed = 10f;

        /// <summary>`DefaultThrottleScaler` on BrittleStar.prefab.</summary>
        public const float ThrottleScaler = 35f;

        /// <summary>`RotationThrottleScaler` on BrittleStar.prefab.</summary>
        public const float RotationThrottleScaler = 0.1f;

        /// <summary>The smaller of `PitchScaler` and `YawScaler` on BrittleStar.prefab.</summary>
        public const float TurnScaler = 80f;

        /// <summary>`speedModeMultiplier.Value` on BrittleStarConfig.asset (x6 at rest, x8 at Time 10).</summary>
        public const float SpeedModeMultiplier = 6f;

        /// <summary>Cruise at full throttle: 45 u/s.</summary>
        public const float CruiseSpeed = MinimumSpeed + ThrottleScaler;

        /// <summary>Speed Mode at rest: 270 u/s.</summary>
        public const float SpeedModeSpeed = CruiseSpeed * SpeedModeMultiplier;

        public static float TurnRateDegPerSec(float speed) => speed * RotationThrottleScaler + TurnScaler;

        public static float CornerRadiusAt(float speed) => speed / (TurnRateDegPerSec(speed) * Mathf.Deg2Rad);

        /// <summary>Speed Mode's circle (~145 u) - the number the floors sit against.</summary>
        public static float SpeedModeRadius => CornerRadiusAt(SpeedModeSpeed);

        /// <summary>The cruise circle (~30 u).</summary>
        public static float CruiseRadius => CornerRadiusAt(CruiseSpeed);

        /// <summary>
        /// The shipped circuit per intensity: Thresher Time's corner profiles made sharper at I2-I4 and
        /// its spreads re-cut for this hull, giving 0 / 1 / 2 / 3 corners a lap tighter than Speed Mode's ~145 u circle (measured over 400 seeds). The floor and the mouths are in this hull's
        /// own units. Eight gates a lap on Headlong's 800 u ring.
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
                    new[] { 125f,  90f,  60f,  40f,  25f,  12f,   5f,   3f },
                    new[] { 150f,  90f,  60f,  40f,  25f,  18f,  12f,   5f },
                    new[] { 160f, 150f,  60f,   6f,   4f,   3f,   2f,   1f },
                    new[] { 165f, 155f, 140f,   6f,   4f,   3f,   2f,   1f },
                }[i - 1],
                // An ABSOLUTE floor in this hull's units, scaled off SpeedModeRadius: no corner is ever
                // tighter than this, whatever the solver does. CornerRadiusFactor stays 0 (it would
                // be read as a fraction of the RHINO's flat-out radius).
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 1.3f, 0.75f, 0.45f, 0.45f }[i - 1] * SpeedModeRadius,
                RadialSwing = new[] { 0.42f, 0.55f, 0.42f, 0.55f }[i - 1],
                AngularSpread = new[] { 1.2f, 6.0f, 3.0f, 4.0f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                RingRadius = new[] { 90f, 68f, 54f, 42f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 70f, 72f, 76f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell.</summary>
        public const float BaseRadius = 800f;
    }
}
