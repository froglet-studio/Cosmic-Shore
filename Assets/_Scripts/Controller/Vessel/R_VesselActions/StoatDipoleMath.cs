using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What is left of the Stoat's FIELD DIPOLE arithmetic (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the autopilot's gauge,
    /// which the swing's AI and the Meerkat's soliton AI judge their target by, and the flight clock's warp step, which
    /// <see cref="StoatPathfinderExecutor"/> writes. The dipole's placement, its field, its flight and its predicted path were
    /// deleted on 2026-11-21 with the dipole (Yash: "delete all other old pathfinding"). Pure functions of their arguments.
    /// </summary>
    public static class StoatDipoleMath
    {
        /// <summary>
        /// The target an autopilot judges: the BEARING of the point it steers for (<paramref name="localSteer"/>, hull space)
        /// at the RANGE of the thing it is after (<paramref name="objectiveRange"/>, a ring). On a race line the steering point
        /// always sits one look-ahead (60-520 u) ahead, inside the autopilot's "far enough to press" test at cruise, so judging
        /// that point alone, an AI Stoat never pressed and raced at cruise.
        /// </summary>
        public static Vector3 AutopilotGauge(Vector3 localSteer, float objectiveRange) =>
            localSteer.sqrMagnitude > 1e-6f ? localSteer.normalized * Mathf.Max(0f, objectiveRange) : localSteer;

        /// <summary>
        /// The warp boost's multiplier: it rises toward <paramref name="boost"/> at <paramref name="riseRate"/> per second
        /// while the flight is warped and falls back to 1 over <paramref name="fadeSeconds"/> once it is not.
        /// </summary>
        public static float StepBoost(float current, bool warped, float boost, float riseRate, float fadeSeconds, float dt)
        {
            current = Mathf.Max(1f, current);
            if (warped) return current + (Mathf.Max(1f, boost) - current) * (1f - Mathf.Exp(-Mathf.Max(0.1f, riseRate) * dt));
            return 1f + (current - 1f) * Mathf.Exp(-dt / Mathf.Max(0.01f, fadeSeconds));
        }
    }
}
