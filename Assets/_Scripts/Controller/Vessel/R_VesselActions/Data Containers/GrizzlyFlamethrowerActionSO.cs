using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grizzly third weapon (Mass 5): the burning plasma-claw flamethrower.
    /// Costs NO energy (per the class doc). Sets enemy prisms — and enemy trails,
    /// which are prisms — on fire with a spreading burn (PrismBurnManager).
    /// With Mass 5 active the burn STEALS on burnout instead of destroying.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyFlamethrowerAction", menuName = "ScriptableObjects/Vessel Actions/Grizzly Flamethrower")]
    public class GrizzlyFlamethrowerActionSO : ShipActionSO
    {
        [Header("Cone")]
        [SerializeField, Tooltip("Reach of the claw's spray ahead of the vessel.")]
        float range = 60f;
        [SerializeField, Tooltip("Half-angle of the ignite cone, degrees.")]
        float coneHalfAngle = 25f;
        [SerializeField, Tooltip("Ignites per second while held.")]
        float igniteTicksPerSecond = 4f;
        [SerializeField, Tooltip("Max prisms ignited per tick.")]
        int ignitesPerTick = 3;

        public float Range => range;
        public float ConeHalfAngle => coneHalfAngle;
        public float IgniteTicksPerSecond => igniteTicksPerSecond;
        public int IgnitesPerTick => ignitesPerTick;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.BeginSpray(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GrizzlyFlamethrowerActionExecutor>()?.EndSpray();
    }
}
