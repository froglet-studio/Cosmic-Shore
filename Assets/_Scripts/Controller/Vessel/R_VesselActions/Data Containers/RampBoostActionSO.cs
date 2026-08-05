using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Boost that builds with CONSTANT ACCELERATION while the triggering input is held
    /// (e.g. the Rhino's full-speed-straight run): the cruise speed climbs at a fixed
    /// rate toward the boosted throttle target instead of the default exponential lerp,
    /// via <see cref="VesselTransformer.SetSpeedTrackingRate"/>. On release the speed
    /// returns to the input-driven throttle speed at a fast constant rate. The visual
    /// sell (FOV + Panini quasi dolly zoom, proportional to live speed) is owned by the
    /// fleet-wide <c>VesselSpeedTunnel</c> platform law (Docs/SPEED_TUNNEL.md), which reads
    /// speed — so it tracks the ramp up and the fast return automatically, and this action
    /// neither wires nor knows about it.
    /// </summary>
    [CreateAssetMenu(fileName = "RampBoostAction", menuName = "ScriptableObjects/Vessel Actions/Ramp Boost")]
    public class RampBoostActionSO : ShipActionSO
    {
        [Header("Ramp")]
        [Tooltip("BoostMultiplier applied while the ramp is engaged — sets the top-speed target " +
                 "the acceleration climbs toward (the TIME elemental multiplier stacks on top in " +
                 "the transformer as usual).")]
        [SerializeField] float maxBoostMultiplier = 4f;

        [Tooltip("Constant acceleration while engaged, in speed units per second.")]
        [SerializeField] float accelerationPerSecond = 40f;

        [Tooltip("Constant deceleration after release, in speed units per second — the fast " +
                 "return to the input-driven throttle speed.")]
        [SerializeField] float returnPerSecond = 400f;

        [Header("Feel")]
        [Tooltip("SFX played once when the ramp engages.")]
        [SerializeField] GameplaySFXCategory engageSFX = GameplaySFXCategory.BoostActivate;

        public float MaxBoostMultiplier => maxBoostMultiplier;
        public float AccelerationPerSecond => accelerationPerSecond;
        public float ReturnPerSecond => returnPerSecond;
        public GameplaySFXCategory EngageSFX => engageSFX;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<RampBoostActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<RampBoostActionExecutor>()?.End();
    }
}
