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
    /// ABSOLUTE degrees here — the profile owns the "5x the sustainable cap" multipliers, so no
    /// cap can ever drift away from the cap it is a multiple of.
    ///
    /// <para>Three spread LEVELS, each reached by a ramp and each held for a plateau: the
    /// sustainable cap, the blow-out cap, and the COLLAPSE cap — a spread so wide the gun is
    /// virtually unusable. The time axis is whatever the caller feeds it: the Sparrow feeds HEAT
    /// (seconds of fire, cooled while the trigger is up), not raw hold time.</para>
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

        /// <summary>The blow-out cap. Must exceed <see cref="MaxHalfAngleDegrees"/> to mean anything.</summary>
        public readonly float BlowoutMaxHalfAngleDegrees;

        /// <summary>Seconds the cone holds at <see cref="BlowoutMaxHalfAngleDegrees"/> before it collapses.</summary>
        public readonly float CollapsePlateauSeconds;

        /// <summary>Degrees per second on the THIRD ramp. Zero disables the collapse entirely.</summary>
        public readonly float CollapseGrowthDegreesPerSecond;

        /// <summary>The final cap. Must exceed <see cref="BlowoutMaxHalfAngleDegrees"/> to mean anything.</summary>
        public readonly float CollapseMaxHalfAngleDegrees;

        public GunSpreadStages(
            float onsetSeconds,
            float growthDegreesPerSecond,
            float maxHalfAngleDegrees,
            float plateauSeconds = 0f,
            float blowoutGrowthDegreesPerSecond = 0f,
            float blowoutMaxHalfAngleDegrees = 0f,
            float collapsePlateauSeconds = 0f,
            float collapseGrowthDegreesPerSecond = 0f,
            float collapseMaxHalfAngleDegrees = 0f)
        {
            OnsetSeconds = onsetSeconds;
            GrowthDegreesPerSecond = growthDegreesPerSecond;
            MaxHalfAngleDegrees = maxHalfAngleDegrees;
            PlateauSeconds = plateauSeconds;
            BlowoutGrowthDegreesPerSecond = blowoutGrowthDegreesPerSecond;
            BlowoutMaxHalfAngleDegrees = blowoutMaxHalfAngleDegrees;
            CollapsePlateauSeconds = collapsePlateauSeconds;
            CollapseGrowthDegreesPerSecond = collapseGrowthDegreesPerSecond;
            CollapseMaxHalfAngleDegrees = collapseMaxHalfAngleDegrees;
        }

        /// <summary>
        /// True when a second ramp is authored. Both halves are required: a rate with no
        /// headroom above the sustainable cap, or headroom with no rate, is the sanctioned
        /// "this gun holds at its cap forever" opt-out — i.e. exactly the pre-blow-out curve.
        /// </summary>
        public bool BlowsOut =>
            BlowoutGrowthDegreesPerSecond > 0f && BlowoutMaxHalfAngleDegrees > MaxHalfAngleDegrees;

        /// <summary>
        /// True when a THIRD ramp is authored on top of a blow-out. Same two-halves rule as
        /// <see cref="BlowsOut"/>, and it requires the blow-out: there is no collapse without a
        /// blow-out cap to collapse from.
        /// </summary>
        public bool Collapses =>
            BlowsOut && CollapseGrowthDegreesPerSecond > 0f
                     && CollapseMaxHalfAngleDegrees > BlowoutMaxHalfAngleDegrees;

        /// <summary>
        /// The widest the cone can ever get: the collapse cap when the profile collapses, else
        /// the blow-out cap when it blows out, else the sustainable cap. The reference the range
        /// falloff (<see cref="GunSpreadMath.RangeFactor"/>) is measured against, so a profile
        /// that switches its later stages off still loses its full authored range at the cap it
        /// actually reaches.
        /// </summary>
        public float FinalMaxHalfAngleDegrees =>
            Collapses ? CollapseMaxHalfAngleDegrees
            : BlowsOut ? BlowoutMaxHalfAngleDegrees
            : Mathf.Max(0f, MaxHalfAngleDegrees);

        /// <summary>Seconds of held fire to climb the FIRST ramp (excludes the onset window).</summary>
        public float RampSeconds =>
            GrowthDegreesPerSecond > 0f ? MaxHalfAngleDegrees / GrowthDegreesPerSecond : 0f;

        /// <summary>Seconds to climb the SECOND ramp. Zero when the profile does not blow out.</summary>
        public float BlowoutRampSeconds =>
            BlowsOut ? (BlowoutMaxHalfAngleDegrees - MaxHalfAngleDegrees) / BlowoutGrowthDegreesPerSecond : 0f;

        /// <summary>Seconds to climb the THIRD ramp. Zero when the profile does not collapse.</summary>
        public float CollapseRampSeconds =>
            Collapses ? (CollapseMaxHalfAngleDegrees - BlowoutMaxHalfAngleDegrees) / CollapseGrowthDegreesPerSecond : 0f;

        /// <summary>Seconds of continuous fire before the cone reaches its FINAL cap.</summary>
        public float SecondsToFullSpread
        {
            get
            {
                if (MaxHalfAngleDegrees <= 0f || GrowthDegreesPerSecond <= 0f) return 0f;

                float t = Mathf.Max(0f, OnsetSeconds) + RampSeconds;
                if (!BlowsOut) return t;

                t += Mathf.Max(0f, PlateauSeconds) + BlowoutRampSeconds;
                if (!Collapses) return t;

                return t + Mathf.Max(0f, CollapsePlateauSeconds) + CollapseRampSeconds;
            }
        }

        /// <summary>
        /// Every point on the time axis where the curve changes phase — the onset window ending,
        /// each ramp reaching its cap, each plateau expiring — in ascending order, ending with
        /// <see cref="SecondsToFullSpread"/>. Zero-length phases are skipped, so no two entries
        /// coincide. This is what a HUD draws its transition marks from, so it is derived here,
        /// beside the curve, rather than re-derived by the view.
        /// </summary>
        /// <param name="into">Cleared, then filled. At most six entries.</param>
        public void CollectPhaseJoins(System.Collections.Generic.List<float> into)
        {
            into.Clear();
            if (MaxHalfAngleDegrees <= 0f || GrowthDegreesPerSecond <= 0f) return;

            float t = 0f;
            void Add(float length)
            {
                if (length <= 0f) return;
                t += length;
                into.Add(t);
            }

            Add(Mathf.Max(0f, OnsetSeconds));
            Add(RampSeconds);
            if (!BlowsOut) return;

            Add(Mathf.Max(0f, PlateauSeconds));
            Add(BlowoutRampSeconds);
            if (!Collapses) return;

            Add(Mathf.Max(0f, CollapsePlateauSeconds));
            Add(CollapseRampSeconds);
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
        /// SIX-part piecewise curve — flat, ramp, plateau, blow-out, plateau, collapse:
        ///
        /// <code>
        ///   1. hold  : 0                                        while t &lt; onset
        ///   2. ramp  : (t-onset) x growth                       up to the sustainable cap
        ///   3. plateau: cap                                     for plateauSeconds
        ///   4. blow-out: cap + excess x blowoutGrowth           up to the blow-out cap
        ///   5. plateau: blow-out cap                            for collapsePlateauSeconds
        ///   6. collapse: blow-out cap + excess x collapseGrowth up to the collapse cap
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
            float blowoutRamp = stages.BlowoutRampSeconds;
            if (blowout < blowoutRamp)
                return stages.MaxHalfAngleDegrees + blowout * stages.BlowoutGrowthDegreesPerSecond;

            // 5. the second plateau — and the terminus for a profile that never collapses.
            if (!stages.Collapses)
                return stages.BlowoutMaxHalfAngleDegrees;

            float collapse = blowout - blowoutRamp - Mathf.Max(0f, stages.CollapsePlateauSeconds);
            if (collapse <= 0f)
                return stages.BlowoutMaxHalfAngleDegrees;

            // 6. the collapse.
            return Mathf.Min(
                stages.CollapseMaxHalfAngleDegrees,
                stages.BlowoutMaxHalfAngleDegrees + collapse * stages.CollapseGrowthDegreesPerSecond);
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
        /// How much of its authored RANGE a round keeps at the current cone, 0..1. Range falls
        /// LINEARLY with the half-angle — proportional to spread — from the full authored range at
        /// a cold gun (half-angle 0) to <paramref name="rangeAtFullSpread"/> of it at
        /// <paramref name="finalMaxHalfAngleDegrees"/>, the widest cap the curve reaches. Past
        /// that cap it holds rather than extrapolating, so a mis-authored cap can never drive
        /// range to zero or negative.
        ///
        /// Linear in ANGLE, not in heat: the plateaus are flat in both, the ramps move both, so
        /// the range a pilot has is always the range the cone they can see implies. It is applied
        /// as a MUZZLE-SPEED factor at the fire site (range = speed x 2T/pi with T fixed), which
        /// keeps the round's flight TIME — and so the number of rounds in the air and the charge
        /// shells' light budget — exactly unchanged.
        /// </summary>
        public static float RangeFactor(float halfAngleDegrees, float finalMaxHalfAngleDegrees, float rangeAtFullSpread)
        {
            if (finalMaxHalfAngleDegrees <= 0f) return 1f;
            float t = Mathf.Clamp01(halfAngleDegrees / finalMaxHalfAngleDegrees);
            return Mathf.Lerp(1f, Mathf.Clamp01(rangeAtFullSpread), t);
        }

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
