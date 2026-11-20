using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure math of the vector flight model's INERTIA outside a drift (<see cref="VesselTransformer"/>'s
    /// inertial grip, the Rhino's oversteer since 2026-11-19): how much of the angle between momentum and the nose
    /// closes in one frame, and the slip a steady turn settles at. Kept apart so a test can hold the numbers.
    /// </summary>
    public static class InertialGripMath
    {
        /// <summary>Fraction of the remaining nose-ward angle closed over <paramref name="dt"/> at
        /// <paramref name="gripPerSecond"/>: <c>1 - e^(-grip dt)</c>, frame-rate independent. 0 grip = 1 (snap).</summary>
        public static float Convergence(float gripPerSecond, float dt)
            => gripPerSecond > 0.0001f ? 1f - Mathf.Exp(-gripPerSecond * Mathf.Max(0f, dt)) : 1f;

        /// <summary>The slip, degrees, a steady turn at <paramref name="turnDegreesPerSecond"/> settles at under
        /// <paramref name="gripPerSecond"/>: the nose leads the travel by turn rate / grip (0 when grip is 0, a snap).</summary>
        public static float SteadySlipDegrees(float turnDegreesPerSecond, float gripPerSecond)
            => gripPerSecond > 0.0001f ? Mathf.Abs(turnDegreesPerSecond) / gripPerSecond : 0f;
    }
}
