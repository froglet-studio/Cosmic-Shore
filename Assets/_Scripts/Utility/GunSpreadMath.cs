using UnityEngine;

namespace CosmicShore.Utility
{
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
    ///      (<c>Random.InitState</c> for the HexRace track), and a gun drawing from it 120
    ///      times a second would make those systems' output depend on how long someone held
    ///      the trigger; and
    ///   2. a hash keeps peers that agree on the shot count agreeing on where the shot went,
    ///      which matters for the turret stance's locally-spawned prisms.
    /// </summary>
    public static class GunSpreadMath
    {
        /// <summary>
        /// The cone's half-angle after <paramref name="heldSeconds"/> of continuous fire.
        ///
        /// Flat zero for the first <paramref name="onsetSeconds"/> — that grace window is what
        /// keeps tapped bursts pin-accurate — then linear at
        /// <paramref name="growthDegreesPerSecond"/> until it saturates at
        /// <paramref name="maxHalfAngleDegrees"/> and stops. The cap is the whole point: past
        /// it the spray would stop being a wider danger zone and start being a worse gun.
        /// </summary>
        public static float HalfAngleDegrees(
            float heldSeconds, float onsetSeconds, float growthDegreesPerSecond, float maxHalfAngleDegrees)
        {
            if (maxHalfAngleDegrees <= 0f || growthDegreesPerSecond <= 0f)
                return 0f;

            float decaying = heldSeconds - Mathf.Max(0f, onsetSeconds);
            if (decaying <= 0f)
                return 0f;

            return Mathf.Min(maxHalfAngleDegrees, decaying * growthDegreesPerSecond);
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
