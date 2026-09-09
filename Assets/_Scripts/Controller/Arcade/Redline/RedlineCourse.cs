using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's circuit, stated against the MANTA's own flight model - every number below is
    /// read off Manta.prefab and MantaAnalogTurnBoostExecutor rather than picked by eye, and the
    /// intensity ladder is derived from them. The solver itself is <see cref="HeadlongCircuit"/>,
    /// shared: what makes this course the Manta's is the CUT, not the generator.
    ///
    /// <para><b>The mode is a question about two triggers.</b> The Manta boosts on the OVERLAP of
    /// its triggers (<c>min(LT, RT)</c>) and turns on their DIFFERENCE (<c>RT - LT</c>), so a
    /// pilot holding one trigger flat and easing the other off is trading boost for yaw on one
    /// linear scale: boost <c>b</c> buys <c>180 x (1 + 3b)</c> u/s and costs <c>60 x (1 - b)</c>
    /// deg/s of trigger yaw. Every corner is therefore one question - <i>how much Soar is this
    /// corner worth?</i> - and <see cref="CornerRadiusAtBoost"/> is the curve it is asked on.
    /// Speed follows the answer with a 1.5/s exponential lag (<c>VesselTransformer.LERP_AMOUNT</c>),
    /// so a boost given up costs about two seconds to win back.</para>
    ///
    /// <para><b>Why the Manta and not another hull.</b> Its cruise is the fleet's fastest
    /// (180 u/s, boosted x4 to 720, x1.3 again at Time 10) and its turning circle is bounded:
    /// with <c>RotationThrottleScaler</c> 0.2 the radius converges on <c>180/(pi x 0.2)</c> =
    /// 286 u, so at full boost it holds a 237 u circle and a Time-10 Manta at 936 u/s only
    /// 247. A course whose corners are cut around that radius is a course this vessel flies
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

        /// <summary>
        /// The shipped circuit per intensity. <b>INTENSITY IS WHAT MIX OF CORNERS A LAP ASKS
        /// FOR</b> - the same rule Headlong records, cut against a different curve:
        ///
        /// <code>
        ///      turn angles (deg), dealt so the big ones sit apart     corners that COST Soar,
        ///                                                            median lap over 400 seeds
        ///   1:  90  72  58  48  38  28  16  10     none - every corner holds full boost   362u
        ///   2: 118  92  66  40  22  12   6   4     one, at 53% of top speed              153u
        ///   3: 135 112  78  22   7   3   2   1     two, at 40% / 57%                 121u  160u
        ///   4: 150 124  86   0   0   0   0   0     two hairpins (31% / 56%) and a KNIFE-EDGE
        ///                                          third at 98% - holdable flat out only by
        ///                                          a pilot who is exact             99u  157u  234u
        /// </code>
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
        /// <para>The safety floor is stated in ABSOLUTE units through
        /// <see cref="HeadlongCircuitSettings.CornerFloorRadius"/>, never as a fraction of the
        /// Rhino's flat-out radius, and never below the 82 u a Manta with one trigger released
        /// can actually fly. A Time-10 Manta (936 u/s) holds 247 u rather than 237 - the curve
        /// is cut for the RESTING vessel, so an element level buys a real edge.</para>
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
                    new[] { 150f, 124f,  86f,   0f,   0f,   0f,   0f,   0f },
                }[i - 1],
                // The SAFETY floor in the Manta's own units (178 / 107 / 100 / 90 u): a corner
                // the solver overshot is still one the Manta holds at a tenth of its boost, and
                // never tighter than the 82 u it can fly with one trigger released. Not the
                // design - CornerProfile is. CornerRadiusFactor is left at 0 on purpose: it
                // would be read as a fraction of the RHINO's flat-out radius.
                CornerRadiusFactor = 0f,
                CornerFloorRadius = new[] { 0.75f, 0.45f, 0.42f, 0.38f }[i - 1] * full,
                RadialSwing = 0.42f,
                // The reach dial (see the class docs): 3.0 is what turns level 2's asked-for
                // 118 degrees into a corner that exists, and 4.8 is what drags level 4's third
                // corner down onto the knife-edge.
                AngularSpread = new[] { 1.2f, 3.0f, 2.8f, 4.8f }[i - 1],
                // More out-of-plane than Headlong at every level (120/170/215/260 there): the
                // swoop is the Manta's corner.
                LateralPerturbation = new[] { 160f, 200f, 240f, 280f }[i - 1],
                // A Manta at 720 u/s crosses a mouth in a sixth of a second; its lateral
                // authority (174 deg/s at that speed) is a quarter of the Rhino's at 1210, so
                // the mouths sit a step wider than Headlong's at every level.
                RingRadius = new[] { 110f, 88f, 72f, 58f }[i - 1],
                AxisJitterDegrees = new[] { 20f, 28f, 36f, 44f }[i - 1],
                // Must COVER half the level's hardest turn (a gate faces its corner's bisector,
                // so the jitter budget is `cap - halfTurn` and clamps to zero past it). The
                // measured worst half-turn over 400 seeds is 44.2 / 67.4 / 71.7 / 71.4, and each
                // cap sits a little over its own.
                MaxPresentDegrees = new[] { 50f, 72f, 76f, 80f }[i - 1],
            };
        }
    }
}
