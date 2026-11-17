using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's circuit, stated against the MANTA's own flight model - every number below is
    /// read off Manta.prefab and MantaAnalogTurnBoostExecutor rather than picked by eye, and the
    /// intensity ladder is derived from them. The solver itself is <see cref="HeadlongCircuit"/>,
    /// shared: what makes this course the Manta's is the CUT, not the generator.
    ///
    /// <para><b>The mode teaches ONE ability: Soar, the Manta's Time ability</b> (Yash,
    /// 2026-11-04: "introduce the new player to the time ability of Manta in intensity 1, and in
    /// intensity 4 it will get intense; no other elemental abilities needed"). The Manta Soars on
    /// the OVERLAP of its triggers (<c>min(LT, RT)</c>): both held at <c>b</c> buy
    /// <c>180 x (1 + 3b)</c> u/s, and easing BOTH together turns it tighter, because its stick's
    /// turn rate grows slower than its speed. Every corner is therefore one question - <i>how much
    /// Soar is this corner worth?</i> - and <see cref="SoarRadius"/> is the curve it is asked on.
    /// No corner is tighter than <see cref="SoarOnlyMinimumRadius"/> (both triggers released, the
    /// stick alone), so the race never needs the trigger DIFFERENCE (Yawstery, the Mass ability,
    /// <see cref="CornerRadiusAtBoost"/>'s 82 u pivot) or a roll into pitch. Speed follows the
    /// answer with a 1.5/s exponential lag (<c>VesselTransformer.LERP_AMOUNT</c>), so Soar given up
    /// costs about two seconds to win back.</para>
    ///
    /// <para><b>Since 2026-11-07 Soar is the Time LEVEL-5 upgrade and Time raises the cruise too</b>, so the race starts
    /// every Manta at Time 6 (<see cref="StartingTimeLevelByIntensity"/>) and is flown on <see cref="SoarSpeed"/>: 1104 u/s
    /// and a 252 u circle at Time 6, 1404 u/s and 259 u at Time 10. The 720 / 237 / 156 numbers below are the curve the
    /// course was CUT on (Soar at Time 0, as it was on 2026-11-04); the course is unchanged, so they stand as its record.</para>
    ///
    /// <para><b>Why the Manta and not another hull.</b> Its cruise is the fleet's fastest
    /// (180 u/s at Time 0, +5% a Time level; Soar x4 on top, +3% a level) and its turning circle is bounded:
    /// with <c>RotationThrottleScaler</c> 0.2 the radius converges on <c>180/(pi x 0.2)</c> =
    /// 286 u, so at full Soar it holds a 237-259 u circle whatever its Time level. A course whose corners are cut around that radius is a course this vessel flies
    /// at the speed the mode is named for, on long swooping legs it can PITCH through - it
    /// pitches (50 deg/s) better than it yaws (30), which is why the course is cut more out of
    /// its plane than Headlong's.</para>
    /// </summary>
    public static class RedlineCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────
        // Read from Manta.prefab (VesselTransformer + VesselStatus) and the boost executor.

        /// <summary>`DefaultThrottleScaler` on Manta.prefab - the cruise speed at full throttle.</summary>
        public const float MantaThrottleScaler = 180f;

        /// <summary>`DefaultMinimumSpeed` on Manta.prefab.</summary>
        public const float MantaMinimumSpeed = 0f;

        /// <summary>`boostMultiplier` on Manta.prefab's VesselStatus - what a full Soar buys.</summary>
        public const float MantaBoostMultiplier = 4f;

        /// <summary>`RotationThrottleScaler` on Manta.prefab.</summary>
        public const float MantaRotationThrottleScaler = 0.2f;

        /// <summary>The smaller of `PitchScaler` (50) and `YawScaler` (30) on Manta.prefab - the
        /// conservative axis, exactly as `VesselTransformer.MaxTurnRateDegreesPerSecond` reads it.
        /// A corner in an arbitrary plane is some blend of the two, so the worse axis is the rate
        /// a course can rely on; a pilot who ROLLS a corner into pitch beats this curve, which is
        /// the skill layer rather than the design.</summary>
        public const float MantaTurnScaler = 30f;

        /// <summary>`maxYawDegPerSec` on Manta.prefab's MantaAnalogTurnBoostExecutor - the yaw a
        /// fully NET trigger buys on top of the stick.</summary>
        public const float MantaTriggerYawDegPerSec = 60f;

        /// <summary>`MantaThrottleScaler x MantaBoostMultiplier + MantaMinimumSpeed`: 720 u/s.</summary>
        public const float MantaTopSpeed = MantaThrottleScaler * MantaBoostMultiplier + MantaMinimumSpeed;

        // ── The CURVE the course is cut against ──────────────────────────────
        // A pilot holding one trigger flat and the other at b: boost = b, net trigger = 1 - b.

        /// <summary>Sustained speed at boost <paramref name="boost"/> in [0,1] with the throttle
        /// buried - the target `VesselTransformer.ComputeThrottleTarget` lerps toward.</summary>
        public static float SpeedAtBoost(float boost)
        {
            float b = Mathf.Clamp01(boost);
            float multiplier = 1f + (MantaBoostMultiplier - 1f) * b;   // MantaAnalogTurnBoostExecutor
            return MantaThrottleScaler * multiplier + MantaMinimumSpeed;
        }

        /// <summary>Yaw rate at boost <paramref name="boost"/>: the stick's speed-scaled term plus
        /// whatever trigger difference the overlap leaves.</summary>
        public static float TurnRateAtBoost(float boost)
        {
            float b = Mathf.Clamp01(boost);
            return SpeedAtBoost(b) * MantaRotationThrottleScaler + MantaTurnScaler
                   + MantaTriggerYawDegPerSec * (1f - b);
        }

        /// <summary>The circle a Manta holds at boost <paramref name="boost"/>: 237 u flat out,
        /// 172 u at half, 82 u with one trigger flat and the other released.</summary>
        public static float CornerRadiusAtBoost(float boost)
        {
            float b = Mathf.Clamp01(boost);
            float v = SpeedAtBoost(b);
            float omega = TurnRateAtBoost(b);
            return omega <= 1e-4f ? float.PositiveInfinity : v / (omega * Mathf.Deg2Rad);
        }

        /// <summary>THE number the course is built around: the tightest corner a Manta holds
        /// WITHOUT giving up any Soar. ~237 u.</summary>
        public static float FullBoostRadius => CornerRadiusAtBoost(1f);

        /// <summary>
        /// The most Soar a Manta can hold through a corner of <paramref name="radius"/>, by
        /// inverting <see cref="CornerRadiusAtBoost"/> (monotone in boost - both the speed and
        /// the loss of trigger yaw grow with it, so the radius does too). 1 for any corner at or
        /// above <see cref="FullBoostRadius"/>.
        /// </summary>
        public static float FastestBoostForCorner(float radius)
        {
            if (radius >= FullBoostRadius) return 1f;
            if (radius <= CornerRadiusAtBoost(0f)) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (CornerRadiusAtBoost(mid) > radius) hi = mid; else lo = mid;
            }
            return lo;
        }

        /// <summary>Speed the Manta settles at through a corner of <paramref name="radius"/>.</summary>
        public static float FastestSpeedForCorner(float radius) =>
            SpeedAtBoost(FastestBoostForCorner(radius));

        // ── The curve the course is CUT against: Soar alone ──────────────────
        // Both triggers eased TOGETHER to s: Soar = s, no trigger difference, so the only turn is the
        // stick's, 0.2 v + 30 deg/s. 237 u flat out, 215 u at half, 156 u with both released.

        /// <summary>The circle a Manta holds with both triggers at <paramref name="soar"/> in [0,1] and
        /// the stick hard over: the Time ability alone, no Yawstery.</summary>
        public static float SoarRadius(float soar)
        {
            float v = SpeedAtBoost(soar);
            float omega = v * MantaRotationThrottleScaler + MantaTurnScaler;
            return omega <= 1e-4f ? float.PositiveInfinity : v / (omega * Mathf.Deg2Rad);
        }

        /// <summary>The tightest corner Soar alone can make: both triggers released, the stick hard
        /// over, at cruise. ~156 u. Every Redline corner is at least this wide.</summary>
        public static float SoarOnlyMinimumRadius => SoarRadius(0f);

        /// <summary>The most Soar a Manta can hold through a corner of <paramref name="radius"/> with
        /// both triggers eased together (<see cref="SoarRadius"/> inverted; monotone in Soar). 1 at or
        /// above <see cref="FullBoostRadius"/>, 0 at or below <see cref="SoarOnlyMinimumRadius"/>.</summary>
        public static float FastestSoarForCorner(float radius)
        {
            if (radius >= SoarRadius(1f)) return 1f;
            if (radius <= SoarOnlyMinimumRadius) return 0f;
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 40; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (SoarRadius(mid) > radius) hi = mid; else lo = mid;
            }
            return lo;
        }

        /// <summary>Speed the Manta settles at through a corner of <paramref name="radius"/> on Soar alone.</summary>
        public static float FastestSoarSpeedForCorner(float radius) =>
            SpeedAtBoost(FastestSoarForCorner(radius));

        // ── The curve as the race is FLOWN: Soar at a Time level (2026-11-11) ─────────────
        // The curves above are the 2026-11-04 cut, made when Soar was live at Time 0 and Time scaled Soar alone. Since
        // 2026-11-07 (96952eb939, c4052cb70e) Soar is the Manta's Time LEVEL-5 upgrade (dark below Time 5, relocked
        // below 4), and Time raises the cruise too. The course is still cut on the curves above (so every seed is the
        // same course it was and the card art stands); the race is FLOWN on these, and the heat grades on them
        // (RedlineHeat). REDLINE.md section 0 has what that changed.

        /// <summary>`ThrottleScalerMultiplier` on Manta.prefab: 1 -> 1.5 over Time 0-10, so +5% cruise a level.</summary>
        public const float MantaCruisePerTimeLevel = 0.05f;

        /// <summary>`BoostSpeedMultiplier` on Manta.prefab: 1 -> 1.3 over Time 0-10, so +3% Soar a level (while boosting).</summary>
        public const float MantaSoarPerTimeLevel = 0.03f;

        /// <summary>The Manta's ability map (Manta.asset, Time): Soar is the level-5 upgrade.</summary>
        public const int MantaSoarUnlockLevel = 5;

        /// <summary>The Manta's ability map (Manta.asset, Time): Soar relocks below this level.</summary>
        public const int MantaSoarRelockBelowLevel = 4;

        /// <summary>The top of the earned band (overcharge past 10 is not read here).</summary>
        public const int MantaMaxTimeLevel = 10;

        /// <summary>
        /// The Time level every Manta starts the race at, I1-I4: the card's <c>StartingElements</c>, written by
        /// <c>Tools/Build/author_redline_assets.py</c> from this line. <b>6 at every level</b>: Soar is lit from the
        /// gun (it unlocks at 5) with two petals of headroom before it relocks (below 4). Before 2026-11-11 the card
        /// seeded nothing, so every Manta, human and AI, started at Time 0 with Soar dark and flew the "crazy fast" race
        /// at 180 u/s until a fifth Time crystal lit it (never at I1).
        /// </summary>
        public static readonly int[] StartingTimeLevelByIntensity = { 6, 6, 6, 6 };

        public static int StartingTimeLevel(int intensity) =>
            StartingTimeLevelByIntensity[Mathf.Clamp(intensity, 1, StartingTimeLevelByIntensity.Length) - 1];

        static int ClampLevel(int timeLevel) => Mathf.Clamp(timeLevel, 0, MantaMaxTimeLevel);

        /// <summary>Is Soar live at <paramref name="timeLevel"/> (climbing; a Manta falling from above relocks below 4)?</summary>
        public static bool SoarLit(int timeLevel) => timeLevel >= MantaSoarUnlockLevel;

        /// <summary>Cruise at <paramref name="timeLevel"/>, throttle buried, no Soar: 180 x (1 + 0.05 L).</summary>
        public static float CruiseAt(int timeLevel) =>
            MantaThrottleScaler * (1f + MantaCruisePerTimeLevel * ClampLevel(timeLevel)) + MantaMinimumSpeed;

        /// <summary>
        /// Speed with both triggers at <paramref name="soar"/> at <paramref name="timeLevel"/>: the cruise, x (1 + 3s) x the
        /// Time level's Soar multiplier while boosting (<c>VesselTransformer.CurrentBoostAmount</c>; the executor boosts
        /// over a 0.01 overlap). Cruise alone while Soar is dark.
        /// </summary>
        public static float SoarSpeed(float soar, int timeLevel)
        {
            float s = Mathf.Clamp01(soar);
            float cruise = CruiseAt(timeLevel);
            if (s <= 0.01f || !SoarLit(timeLevel)) return cruise;
            return cruise * (1f + (MantaBoostMultiplier - 1f) * s) * (1f + MantaSoarPerTimeLevel * ClampLevel(timeLevel));
        }

        /// <summary>Full Soar at <paramref name="timeLevel"/>: 1104 u/s at Time 6, 1404 at Time 10.</summary>
        public static float SoarTop(int timeLevel) => SoarSpeed(1f, timeLevel);

        /// <summary>The circle the stick alone holds at <paramref name="speed"/> (0.2 v + 30 deg/s, no trigger difference).</summary>
        public static float StickRadiusAt(float speed)
        {
            float omega = speed * MantaRotationThrottleScaler + MantaTurnScaler;
            return omega <= 1e-4f ? float.PositiveInfinity : speed / (omega * Mathf.Deg2Rad);
        }

        /// <summary>Both triggers at <paramref name="soar"/> at <paramref name="timeLevel"/>, stick hard over.</summary>
        public static float SoarRadius(float soar, int timeLevel) => StickRadiusAt(SoarSpeed(soar, timeLevel));

        /// <summary>Flat out at <paramref name="timeLevel"/>: 252 u at Time 6, 259 at Time 10.</summary>
        public static float FullSoarRadius(int timeLevel) => SoarRadius(1f, timeLevel);

        /// <summary>Both triggers released, the stick alone, at cruise: 175 u at Time 6, 184 at Time 10. A corner tighter
        /// than this needs the trigger DIFFERENCE (Yawstery, the Manta's Time turn).</summary>
        public static float StickAloneRadius(int timeLevel) => StickRadiusAt(CruiseAt(timeLevel));

        /// <summary>
        /// The shipped circuit per intensity. <b>INTENSITY IS WHAT MIX OF CORNERS A LAP ASKS
        /// FOR</b> - the same rule Headlong records, cut against the Soar-only curve
        /// (<see cref="SoarRadius"/>): I1 teaches Soar, I4 is intense, and no level needs another
        /// ability (2026-11-04, REDLINE.md section 4):
        ///
        /// <code>
        ///      turn angles (deg), dealt so the big ones sit apart     corners that COST Soar (Soar held),
        ///                                                            median lap over 400 seeds
        ///   1:  90  72  58  48  38  28  16  10     none: bury both triggers               362u
        ///   2: 118  92  66  40  22  12   6   4     one, eased to half                     217u (53%)
        ///   3: 135 112  78  22   7   3   2   1     two, a short leg apart          184u (17%)  228u (76%)
        ///   4: 125 120 115   0   0   0   0   0     three: triggers nearly off, a third, and a
        ///                                          KNIFE-EDGE held flat only by a pilot who is
        ///                                          exact                   171u (8%)  199u (30%)  233u (88%)
        /// </code>
        ///
        /// <para>Before 2026-11-04 the hardest corners were 153 / 121 / 99 u at I2-I4, cut against the
        /// Soar-AND-Yawstery curve (<see cref="CornerRadiusAtBoost"/>): below the 156 u Soar alone can
        /// make, so they asked for the one-trigger pivot (Mass) or a roll into pitch. Level 4's three
        /// even corners (125/120/115) are what keep it at three costing corners once every corner
        /// must clear 160 u: spreading the turn over four or five corners made all of them WIDER
        /// (longer legs, the same turn), and today's 150/124/86 broke the floor and relaxed the whole
        /// lap down to one costing corner (measured on the 400-seed sweep in course_port.py).</para>
        ///
        /// <para>Each row sums to 360 because a closed lap does. The demand is asserted by
        /// <c>RedlineCourseTests</c> over a 400-seed sweep (a floor is a permission, not a
        /// demand - HEADLONG.md §1). What the ladder is made of is NOT only the profile: the
        /// solver's reach at a given profile is bounded by <c>AngularSpread</c> (how far two
        /// gates may be pulled together), and that is the dial that actually moved level 2 from
        /// zero costing corners to one - a profile can ask for a 118-degree corner, but only a
        /// gap squeezed hard enough produces it. Measured, not reasoned: raising the profile's
        /// top angle at a fixed spread changed nothing, and a smaller base circle made every
        /// corner WIDER (shorter legs, same turn - the cancellation FlatRing documents).</para>
        ///
        /// <para>Eight gates a lap, not six: six was measured and rejected (longer legs at the
        /// same turn are bigger circles, so even level 4 lost its third corner). The Manta's
        /// runway is the legs the solver leaves long - 466-868 u at level 4, a second or more
        /// flat out - and the out-of-plane swing is larger than Headlong's at every level because
        /// the Manta pitches better than it yaws: a swooping climb is the corner this hull is
        /// best at.</para>
        ///
        /// <para>The floor is stated in ABSOLUTE units through
        /// <see cref="HeadlongCircuitSettings.CornerFloorRadius"/>, never as a fraction of the
        /// Rhino's flat-out radius, and never below <see cref="SoarOnlyMinimumRadius"/> (156 u): every
        /// corner of every seed can be made by letting Soar go. At I2-I4 it is also the level's
        /// lesson (205 / 175 / 160 u: no corner tighter than half Soar, a sixth, triggers off). A
        /// Time-10 Manta (936 u/s) holds 247 u rather than 237 - the curve is cut for the RESTING
        /// vessel, so a Time crystal (one level, +3% Soar) buys speed and asks for more lift in the
        /// corners (RedlineTimeCrystals).</para>
        /// </summary>
        public static HeadlongCircuitSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            float full = FullBoostRadius;
            return new HeadlongCircuitSettings
            {
                GateCount = 8,
                // A regular octagon at 820 has 628u legs and 45 degree corners, which fits a
                // 758u turn - 3.2x the full-boost radius. The relaxation's base case, and why
                // the generator can never fail.
                BaseRadius = 820f,
                CornerProfile = new[]
                {
                    new[] {  90f,  72f,  58f,  48f,  38f,  28f,  16f,  10f },
                    new[] { 118f,  92f,  66f,  40f,  22f,  12f,   6f,   4f },
                    new[] { 135f, 112f,  78f,  22f,   7f,   3f,   2f,   1f },
                    new[] { 125f, 120f, 115f,   0f,   0f,   0f,   0f,   0f },
                }[i - 1],
                // The floor in the Manta's own units (178 / 205 / 175 / 160 u): the tightest corner
                // a level may ever deal, each at or above the 156 u Soar alone makes, so no level
                // needs Yawstery. At I2-I4 it is what sets the hardest corner (the profile asks for
                // more; the relaxation stops at the floor). CornerRadiusFactor is left at 0 on
                // purpose: it would be read as a fraction of the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.75f * full, 205f, 175f, 160f }[i - 1],
                RadialSwing = 0.42f,
                // The reach dial (see the class docs): 3.0 is what turns level 2's asked-for
                // 118 degrees into a corner that exists, and 4.8 is what drags level 4's third
                // corner down onto the knife-edge (233 u with the 125/120/115 profile).
                AngularSpread = new[] { 1.2f, 3.0f, 2.8f, 4.8f }[i - 1],
                // More out-of-plane than Headlong at every level (120/170/215/260 there): the
                // swoop is the Manta's corner.
                LateralPerturbation = new[] { 160f, 200f, 240f, 280f }[i - 1],
                // A Manta at 720 u/s crosses a mouth in a sixth of a second; its lateral
                // authority (174 deg/s at that speed) is a quarter of the Rhino's at 1200, so
                // levels 1-3 sit a step wider than Headlong's (96 / 72 / 58).
                //
                // Level 4 does NOT: at 44 it is tighter than Headlong's hardest mouth (46) and
                // the ladder's biggest single step (72 -> 44 against 110 -> 88 -> 72). That is
                // the point of the rung - it is the level that already carries the knife-edge
                // corner, and a mouth a Manta can miss is the only thing that makes flat-out a
                // decision rather than a default. It stays well clear of the floor a mouth
                // actually has: Switchback's level 4 is 4.29 (a Dolphin hull x 1.5) and
                // Breakwater's tightest port is 42, so 44 is a demanding gate, not a trick one.
                RingRadius = new[] { 110f, 88f, 72f, 44f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must COVER half the level's hardest turn (a gate faces its corner's bisector,
                // so the jitter budget is `cap - halfTurn` and clamps to zero past it). The
                // measured worst half-turn over 400 seeds is 44.2 / 52.6 / 58.8 / 66.4 (2026-11-04
                // ladder), and each cap sits over its own.
                MaxPresentDegrees = new[] { 50f, 72f, 76f, 80f }[i - 1],
            };
        }
    }
}
