using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's circuit, stated against the SERPENT's flight model (Serpent.prefab and
    /// ConsumeBoostAction.asset; SerpentTimeCourseTests reads them back). The solver is
    /// <see cref="HeadlongCircuit"/>, shared: what makes this course the Serpent's is the CUT.
    ///
    /// <para><b>Speed is pellets.</b> Cruise is <c>10 + 50</c> = 60 u/s. A burning pellet sets the
    /// boost multiplier to 3; n overlapping pellets to <c>1 + 2n</c> (3 / 5 / 7 / 9x), so speed is
    /// <c>50 x multiplier + 10</c>: 160, 260, 360, 460 u/s. The turn rate GROWS with speed
    /// (<c>0.4 x speed + 80</c> deg/s), so the circle grows only from 33 u cruising to ~100 u on four
    /// pellets - the Serpent can carry a stack through most corners. What it cannot carry is the
    /// tank: four pellets, 3 s each, one back every ~3.6 s. A lap is ~5,000 u; burning all four at
    /// once covers ~1,400 u of it. The ladder tightens the mouths and the presentation, so a stack
    /// arriving at 460 u/s has to be aimed.</para>
    /// </summary>
    public static class SerpentTimeCourse
    {
        // ── The vessel this course is cut for (SerpentTimeCourseTests reads each back off its asset) ──

        /// <summary>`DefaultMinimumSpeed` on Serpent.prefab.</summary>
        public const float MinimumSpeed = 10f;

        /// <summary>`DefaultThrottleScaler` on Serpent.prefab.</summary>
        public const float ThrottleScaler = 50f;

        /// <summary>`RotationThrottleScaler` on Serpent.prefab: 0.4 deg/s more turn per u/s.</summary>
        public const float RotationThrottleScaler = 0.4f;

        /// <summary>The smaller of `PitchScaler` and `YawScaler` on Serpent.prefab.</summary>
        public const float TurnScaler = 80f;

        /// <summary>`boostMultiplier` on ConsumeBoostAction.asset - ONE pellet's multiplier.</summary>
        public const float PelletMultiplier = 3f;

        /// <summary>`boostDuration` on ConsumeBoostAction.asset - one pellet's burn, s.</summary>
        public const float PelletBurnSeconds = 3f;

        /// <summary>Pellets the tank holds: `maxAmount` 1 / `resourceCost` 0.25.</summary>
        public const int TankPellets = 4;

        /// <summary>Speed with <paramref name="pellets"/> burning at once (0..4): 60, 160, 260, 360, 460.</summary>
        public static float SpeedWithPellets(int pellets)
        {
            int n = Mathf.Clamp(pellets, 0, TankPellets);
            float multiplier = n == 0 ? 1f : 1f + (PelletMultiplier - 1f) * n;
            return ThrottleScaler * multiplier + MinimumSpeed;
        }

        public static float TurnRateDegPerSec(float speed) => speed * RotationThrottleScaler + TurnScaler;

        public static float CornerRadiusAt(float speed) => speed / (TurnRateDegPerSec(speed) * Mathf.Deg2Rad);

        /// <summary>The circle on a full stack (~100 u) - the number the floors sit against.</summary>
        public static float FullStackRadius => CornerRadiusAt(SpeedWithPellets(TankPellets));

        /// <summary>The cruise circle (~33 u).</summary>
        public static float CruiseRadius => CornerRadiusAt(SpeedWithPellets(0));

        /// <summary>
        /// The shipped circuit per intensity: Thresher Time's corner profiles made sharper at I2-I4 and
        /// its spreads re-cut for this hull, giving 0 / 0 / 2 / 2 corners a lap tighter than a full stack's ~100 u circle (measured over 400 seeds) - the Serpent's ladder is mostly the MOUTH, because its turn grows with its speed. The floor and the mouths are in this hull's
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
                // An ABSOLUTE floor in this hull's units, scaled off FullStackRadius: no corner is ever
                // tighter than this, whatever the solver does. CornerRadiusFactor stays 0 (it would
                // be read as a fraction of the RHINO's flat-out radius).
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 2.0f, 0.75f, 0.55f, 0.55f }[i - 1] * FullStackRadius,
                RadialSwing = new[] { 0.42f, 0.55f, 0.42f, 0.55f }[i - 1],
                AngularSpread = new[] { 1.2f, 6.0f, 6.0f, 6.0f }[i - 1],
                LateralPerturbation = new[] { 120f, 170f, 215f, 260f }[i - 1],
                // One ring a gate, sized for the Serpent's large hull (Shombith, 2026-10-31: "a little bigger ... intensity 4 scale
                // is fine"). Was 90 / 68 / 52 / 40, flown as clusters of 54 / 41 u rings at I1-I2.
                RingRadius = new[] { 65f, 55f, 48f, 40f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                MaxPresentDegrees = new[] { 50f, 80f, 80f, 82f }[i - 1],
            };
        }

        /// <summary>Rings per lap. The race target is <c>laps x GatesPerLap</c>.</summary>
        public const int GatesPerLap = 8;

        /// <summary>The circuit's base circle - Headlong's octagon at 800, mid-shell.</summary>
        public const float BaseRadius = 800f;
    }
}
