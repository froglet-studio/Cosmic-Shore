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
        float range = 60f;
        [SerializeField, Tooltip("Half-angle of the ignite cone at FULL pressure, degrees.")]
        float coneHalfAngle = 25f;
        [SerializeField, Tooltip("Reach of the spray at the LIGHTEST pull.")]
        float minRange = 25f;
        [SerializeField, Tooltip("Half-angle of the cone at the LIGHTEST pull, degrees.")]
        float minConeHalfAngle = 12f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("Max prisms ignited per tick, per cone.")]
        int ignitesPerTick = 3;

        [Header("Muzzles (one per trigger)")]
        [SerializeField, Tooltip("How far each cone's apex sits to its own side of the hull's centre line, in the hull's local units (LT left, RT right).")]
        float sideOffset = 3f;
        [SerializeField, Tooltip("How far ahead of the hull's pivot each cone's apex sits, in the hull's local units.")]
        float noseOffset = 4f;

        public float Range => range;
        public float ConeHalfAngle => coneHalfAngle;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;
        public int IgnitesPerTick => ignitesPerTick;
        public float SideOffset => sideOffset;
        public float NoseOffset => noseOffset;

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
