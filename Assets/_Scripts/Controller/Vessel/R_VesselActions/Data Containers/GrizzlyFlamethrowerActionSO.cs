using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly third weapon (Mass 5): the burning plasma-claw flamethrower.
    /// Costs NO energy (per the class doc). Sets enemy prisms — and enemy trails,
    /// which are prisms — on fire with a spreading burn (PrismBurnManager).
    /// With Mass 5 active the burn STEALS on burnout instead of destroying.
    ///
    /// On the triggers (design ask, 2026-10-16) LT sprays from the LEFT front of the hull and RT
    /// from the RIGHT front, each forward, and the trigger's live PRESSURE sizes its cone between
    /// the Min and Max values below. The Max range must stay the Grizzly's shortest reach - the
    /// claw is always its closest-range weapon.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyFlamethrowerAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Flamethrower")]
    public class GrizzlyFlamethrowerActionSO : ShipActionSO
    {
        [Header("Cone")]
        [SerializeField, Tooltip("Reach of the claw's spray at FULL pressure (and on devices with no analog trigger). Keep it the Grizzly's shortest-range weapon.")]
        float range = 45f;
        [SerializeField, Tooltip("Half-angle of the ignite cone at FULL pressure, degrees.")]
        float coneHalfAngle = 20f;
        [SerializeField, Tooltip("Reach of the spray at the LIGHTEST pull.")]
        float minRange = 18f;
        [SerializeField, Tooltip("Half-angle of the cone at the LIGHTEST pull, degrees.")]
        float minConeHalfAngle = 10f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("Max prisms ignited per tick, per cone.")]
        int ignitesPerTick = 3;

        [Header("Muzzles (one per trigger)")]
        [SerializeField, Tooltip("How far each cone's apex sits to its own side of the hull's centre line, in the hull's local units (LT left, RT right).")]
        float sideOffset = 6f;
        [SerializeField, Tooltip("How far ahead of the hull's pivot each cone's apex sits, in the hull's local units.")]
        float noseOffset = 4f;
        [SerializeField, Tooltip("Degrees each cone is turned OUTWARD from straight ahead (LT left, RT right), so the two claws spread apart. 0 = both straight forward.")]
        float splayDegrees = 6f;

        [Header("Energy cell (one per cone)")]
        [SerializeField, Tooltip("Seconds a FULL cell lasts spraying the SMALLEST cone. Each trigger's cone has its own cell.")]
        float cellSeconds = 3f;
        [SerializeField, Tooltip("How much faster a cell empties at FULL pressure than at the lightest pull (design ask, 2026-10-16: 1.5x for larger flames). Scales linearly with pressure in between.")]
        float largeFlameDrainMultiplier = 1.5f;
        [SerializeField, Tooltip("Seconds an EMPTY cell takes to refill completely while its trigger is not spraying.")]
        float cellRechargeSeconds = 2f;
        [SerializeField, Tooltip("Seconds after a cone stops spraying before its cell starts refilling.")]
        float cellRechargeDelay = 0.25f;

        public float Range => range;
        public float ConeHalfAngle => coneHalfAngle;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;
        public int IgnitesPerTick => ignitesPerTick;
        public float SideOffset => sideOffset;
        public float NoseOffset => noseOffset;
        public float SplayDegrees => splayDegrees;
        public float CellRechargeDelay => cellRechargeDelay;

        /// <summary>Cell fraction spent per second spraying at pressure 0..1: the smallest cone
        /// empties a full cell in <c>cellSeconds</c>, the largest <c>largeFlameDrainMultiplier</c>
        /// times faster.</summary>
        public float CellDrainPerSecond(float pressure01) =>
            Mathf.Lerp(1f, Mathf.Max(1f, largeFlameDrainMultiplier), Mathf.Clamp01(pressure01)) /
            Mathf.Max(0.05f, cellSeconds);

        /// <summary>Cell fraction refilled per second while the cone is idle.</summary>
        public float CellRechargePerSecond => 1f / Mathf.Max(0.05f, cellRechargeSeconds);

        /// <summary>Cone reach for a trigger pressure 0..1 (1 = full).</summary>
        public float RangeForPressure(float pressure01) =>
            Mathf.Lerp(Mathf.Min(minRange, range), range, Mathf.Clamp01(pressure01));

        /// <summary>Cone half-angle for a trigger pressure 0..1 (1 = full).</summary>
        public float HalfAngleForPressure(float pressure01) =>
            Mathf.Lerp(Mathf.Min(minConeHalfAngle, coneHalfAngle), coneHalfAngle, Mathf.Clamp01(pressure01));

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.BeginSpray(this, vesselStatus, null);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.EndSpray(null);
    }
}
