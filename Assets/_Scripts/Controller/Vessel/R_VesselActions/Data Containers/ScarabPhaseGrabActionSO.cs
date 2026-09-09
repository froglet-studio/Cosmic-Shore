using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Config for the Scarab's <b>Phase Grab</b> — hold to reverse a ball you strike and to stop
    /// impeding it once you have. Behaviour and rationale live on
    /// <see cref="ScarabPhaseGrabExecutor"/>; this asset exists so the hold can be BOUND like any
    /// other ability, which is what gives it the server round-trip it needs.
    ///
    /// It carries no tuning of its own on purpose. A phase is either held or it is not, and the
    /// two numbers the grab does need — how slowly a ball may be moving and still be worth
    /// reversing, and how long the pass-through may last — belong to the BALL
    /// (<c>AstroLeagueSettingsSO</c>), because they describe the payload rather than the hand.
    /// </summary>
    [CreateAssetMenu(fileName = "ScarabPhaseGrabAction",
                     menuName = "ScriptableObjects/Vessel Actions/Scarab Phase Grab")]
    public class ScarabPhaseGrabActionSO : ShipActionSO
    {
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ScarabPhaseGrabExecutor>()?.Engage();

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<ScarabPhaseGrabExecutor>()?.Release();
    }
}
