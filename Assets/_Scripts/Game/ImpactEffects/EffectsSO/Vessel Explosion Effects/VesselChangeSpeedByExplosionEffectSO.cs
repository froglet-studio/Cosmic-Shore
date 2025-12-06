using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeSpeedByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselChangeSpeedByExplosionEffectSO")]
    public class VesselChangeSpeedByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Events")]
        [SerializeField]
        private ScriptableEventExplosionDebuffApplied explosionDebuffAppliedEvent;

        [SerializeField]
        private ScriptableEventVesselImpactor vesselSlowedByExplosionEvent;

        [Header("Victim Input Suppression")]
        [SerializeField] private InputEvents inputToMute   = InputEvents.RightStickAction;
        [SerializeField] private float       muteSeconds   = 3f;
        [SerializeField] private bool        forceStopIfActive = true;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            var victimVessel = impactor?.Vessel;
            var victimStatus = victimVessel?.VesselStatus;
            if (victimStatus == null)
                return;

            var handler = victimStatus.ActionHandler;
            if (handler != null)
            {
                handler.MuteInput(inputToMute, muteSeconds);

                if (forceStopIfActive)
                    handler.StopShipControllerActions(inputToMute);
            }

            if (explosionDebuffAppliedEvent != null)
            {
                var payload = new ExplosionDebuffPayload(victimVessel, muteSeconds);
                explosionDebuffAppliedEvent.Raise(payload);
            }

            vesselSlowedByExplosionEvent?.Raise(impactor);
        }
    }
}
