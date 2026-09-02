using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The authored shape of a full-auto gun's accuracy decay, as the pure numbers the curve
    /// needs — a parameter object rather than six loose floats, because the two ramps and the
    /// two caps are trivially transposable at a call site and a transposed pair produces a
    /// plausible wrong curve rather than an error.
    ///
    /// Built from <c>GunSpreadProfile</c> (the authoring surface); consumed by
    /// <see cref="GunSpreadMath.HalfAngleDegrees(float, in GunSpreadStages)"/>. Caps are
    /// ABSOLUTE degrees here — the profile owns the "5x the sustainable cap" multiplier, so the
    /// blow-out cap can never drift away from the cap it is a multiple of.
    /// </summary>
    public readonly struct GunSpreadStages
    {
        /// <summary>Seconds of PERFECT accuracy at the start of every trigger pull.</summary>
        public readonly float OnsetSeconds;

        /// <summary>Degrees of half-angle gained per second on the first ramp.</summary>
        public readonly float GrowthDegreesPerSecond;

        /// <summary>The SUSTAINABLE cap — the height of the plateau the first ramp climbs to.</summary>
        public readonly float MaxHalfAngleDegrees;

        /// <summary>Seconds the cone holds at <see cref="MaxHalfAngleDegrees"/> before it blows out.</summary>
        public readonly float PlateauSeconds;

        /// <summary>Degrees per second on the second ramp. Zero disables the blow-out entirely.</summary>
        public readonly float BlowoutGrowthDegreesPerSecond;

        /// <summary>The final cap. Must exceed <see cref="MaxHalfAngleDegrees"/> to mean anything.</summary>
        public readonly float BlowoutMaxHalfAngleDegrees;

        public GunSpreadStages(
            float onsetSeconds,
            float growthDegreesPerSecond,
            float maxHalfAngleDegrees,
            float plateauSeconds = 0f,
            float blowoutGrowthDegreesPerSecond = 0f,
            float blowoutMaxHalfAngleDegrees = 0f)
        {
            OnsetSeconds = onsetSeconds;
            GrowthDegreesPerSecond = growthDegreesPerSecond;
            MaxHalfAngleDegrees = maxHalfAngleDegrees;
            PlateauSeconds = plateauSeconds;
            BlowoutGrowthDegreesPerSecond = blowoutGrowthDegreesPerSecond;
            BlowoutMaxHalfAngleDegrees = blowoutMaxHalfAngleDegrees;
        }

        /// <summary>
        /// True when a second ramp is authored. Both halves are required: a rate with no
        /// headroom above the sustainable cap, or headroom with no rate, is the sanctioned
        /// "this gun holds at its cap forever" opt-out — i.e. exactly the pre-blow-out curve.
        /// </summary>
        public bool BlowsOut =>
            BlowoutGrowthDegreesPerSecond > 0f && BlowoutMaxHalfAngleDegrees > MaxHalfAngleDegrees;

        /// <summary>Seconds of held fire to climb the FIRST ramp (excludes the onset window).</summary>
        public float RampSeconds =>
            GrowthDegreesPerSecond > 0f ? MaxHalfAngleDegrees / GrowthDegreesPerSecond : 0f;

        /// <summary>Seconds of continuous fire before the cone reaches its FINAL cap.</summary>
        public float SecondsToFullSpread
        {
            get
            {
                if (MaxHalfAngleDegrees <= 0f || GrowthDegreesPerSecond <= 0f) return 0f;

                float t = Mathf.Max(0f, OnsetSeconds) + RampSeconds;
                if (!BlowsOut) return t;

                return t + Mathf.Max(0f, PlateauSeconds)
                         + (BlowoutMaxHalfAngleDegrees - MaxHalfAngleDegrees) / BlowoutGrowthDegreesPerSecond;
            }
        }
    }

    /// <summary>
    /// The pure math behind a full-auto gun's accuracy decay: how wide the cone is after
    /// holding the trigger for a given time, and where inside that cone one round goes.
    ///
    /// Deliberately static and side-effect free so it can be edit-mode tested
    /// (<c>GunSpreadMathTests</c>) and so the two Sparrow fire modes — bullets and turret
    /// prisms — share one implementation instead of authoring the cone twice.
    ///
    /// **It does not touch <see cref="UnityEngine.Random"/>.** The perturbation is a pure
    /// hash of a caller-supplied shot serial, for two reasons:
    ///   1. the global RNG stream is shared state that deterministic systems seed
    ///      (<c>Random.InitState</c> for the SkimRace track), and a gun drawing from it 120
    ///      times a second would make those systems' output depend on how long someone held
    ///      the trigger; and
    ///   2. a hash keeps peers that agree on the shot count agreeing on where the shot went,
    ///      which matters for the turret stance's locally-spawned prisms.
    /// </summary>
    public static class GunSpreadMath
    {
        /// <summary>
        /// The cone's half-angle after <paramref name="heldSeconds"/> of continuous fire, as a
        /// FOUR-part piecewise curve — flat, ramp, plateau, blow-out:
        ///
        /// <code>
        ///   1. hold  : 0                                        while t &lt; onset
        ///   2. ramp  : (t-onset) x growth                       up to the sustainable cap
        ///   3. plateau: cap                                     for plateauSeconds
        ///   4. blow-out: cap + excess x blowoutGrowth           up to the blow-out cap
        /// </code>
        ///
        /// The grace window is what keeps tapped bursts pin-accurate. The plateau is the
        /// SUSTAINABLE band — wide enough to saturate a danger zone, narrow enough that a held
        /// burst still kills what it is pointed at — and the blow-out past it is the price of
        /// never letting go: the second ramp is authored FASTER than the first, so the failure
        /// accelerates and the gun stops being a weapon you can aim at all.
        ///
        /// Continuous at all three joins by construction, and monotonic non-decreasing everywhere.
        /// A profile with no blow-out authored holds at the cap forever, which is exactly the
        /// single-ramp curve this replaced.
        /// </summary>
        public static float HalfAngleDegrees(float heldSeconds, in GunSpreadStages stages)
        {
            if (stages.MaxHalfAngleDegrees <= 0f || stages.GrowthDegreesPerSecond <= 0f)
                return 0f;

            // 1. the grace window.
            float decaying = heldSeconds - Mathf.Max(0f, stages.OnsetSeconds);
            if (decaying <= 0f)
                return 0f;

            // 2. the first ramp.
            float rampSeconds = stages.RampSeconds;
            if (decaying < rampSeconds)
                return decaying * stages.GrowthDegreesPerSecond;

            // 3. the plateau — and the terminus for a profile that never blows out.
            if (!stages.BlowsOut)
                return stages.MaxHalfAngleDegrees;

            float blowout = decaying - rampSeconds - Mathf.Max(0f, stages.PlateauSeconds);
            if (blowout <= 0f)
                return stages.MaxHalfAngleDegrees;

            // 4. the blow-out.
            return Mathf.Min(
                stages.BlowoutMaxHalfAngleDegrees,
                stages.MaxHalfAngleDegrees + blowout * stages.BlowoutGrowthDegreesPerSecond);
        }

        /// <summary>
        /// The single-ramp curve: hold, ramp, hard cap, forever. Kept as the shorthand for a gun
        /// that authors no blow-out (and as the shape every pre-blow-out caller expected) —
        /// it is exactly <see cref="HalfAngleDegrees(float, in GunSpreadStages)"/> over a
        /// <see cref="GunSpreadStages"/> whose second stage is switched off.
        /// </summary>
        public static float HalfAngleDegrees(
            float heldSeconds, float onsetSeconds, float growthDegreesPerSecond, float maxHalfAngleDegrees)
            => HalfAngleDegrees(
                heldSeconds,
                new GunSpreadStages(onsetSeconds, growthDegreesPerSecond, maxHalfAngleDegrees));

        /// <summary>
        /// One round's direction: <paramref name="forward"/> deflected to a point inside a cone
        /// of <paramref name="halfAngleDegrees"/>, chosen from the hash of
        /// <paramref name="shotSerial"/>. Always returns a unit vector.
        ///
        /// <paramref name="distributionBias"/> shapes where inside the cone rounds land, by
        /// sampling the deflection as <c>maxAngle * u^bias</c>:
        ///   • <b>0.5</b> — uniform over the cone's disc: the whole danger zone saturates
        ///     evenly. This is the default and the one the design asks for.
        ///   • <b>1.0</b> — density falls off as 1/r: a tight core with a thin halo, so the
        ///     thing you are actually aiming at still takes most of the rounds.
        ///   • <b>&lt; 0.5</b> — hollows the middle out toward the rim. Rarely what you want.
        /// </summary>
        public static Vector3 Perturb(Vector3 forward, float halfAngleDegrees, float distributionBias, uint shotSerial)
        {
            Vector3 axis = forward.sqrMagnitude > 1e-12f ? forward.normalized : Vector3.forward;
            if (halfAngleDegrees <= 0f)
                return axis;

            float u = UnitFloat(Hash(shotSerial));
            float v = UnitFloat(Hash(shotSerial ^ 0x9E3779B9u));

            float deflection = Mathf.Deg2Rad * halfAngleDegrees * Mathf.Pow(u, Mathf.Max(0.05f, distributionBias));
            float roll = v * 2f * Mathf.PI;

            // Orthonormal basis about the aim axis. The helper swaps near the poles so the
            // cross product can never degenerate.
            Vector3 helper = Mathf.Abs(axis.y) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 right = Vector3.Cross(helper, axis).normalized;
            Vector3 up = Vector3.Cross(axis, right);

            Vector3 radial = right * Mathf.Cos(roll) + up * Mathf.Sin(roll);
            return (axis * Mathf.Cos(deflection) + radial * Mathf.Sin(deflection)).normalized;
        }

        /// <summary>
        /// The rotation that carries <paramref name="from"/> onto <paramref name="to"/> — used
        /// to deflect a muzzle's pose by exactly the shot's spread while PRESERVING its roll
        /// (rebuilding the rotation with <c>LookRotation</c> would silently re-reference roll
        /// to world up, which matters for a turret prism whose long axis is the shot).
        /// </summary>
        public static Quaternion DeflectionOf(Vector3 from, Vector3 to) => Quaternion.FromToRotation(from, to);

        // A standard integer avalanche hash (Wang/Jenkins style). Any two serials one apart
        // produce uncorrelated outputs, which is what makes consecutive rounds scatter.
        static uint Hash(uint x)
        {
            unchecked
            {
                x ^= 2747636419u; x *= 2654435769u;
                x ^= x >> 16;     x *= 2654435769u;
                x ^= x >> 16;     x *= 2654435769u;
                return x;
            }
        }

        // 24 bits is plenty of resolution for an angle and keeps the divide exact in float.
        static float UnitFloat(uint hash) => (hash & 0x00FFFFFFu) / 16777216f;
    }
}
