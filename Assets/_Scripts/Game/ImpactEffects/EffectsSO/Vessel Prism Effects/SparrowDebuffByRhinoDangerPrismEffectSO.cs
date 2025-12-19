using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SparrowDebuffByRhinoDangerPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/SparrowDebuffByRhinoDangerPrismEffectSO")]
    public sealed class SparrowDebuffByRhinoDangerPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Events")]
        [SerializeField, Tooltip("Raised when a Rhino danger prism slows a vessel (impactor context).")]
        private ScriptableEventVesselImpactor vesselSlowedByRhinoDangerPrismEvent;

        [SerializeField, Tooltip("Raised when this effect applies an explosion-style debuff (victim + duration).")]
        private ScriptableEventExplosionDebuffApplied explosionDebuffAppliedEvent;

        [Header("Debuff Settings")]
        [SerializeField, Tooltip("Which input to block on the victim.")]
        private InputEvents inputToMute = InputEvents.Button2Action;

        [SerializeField, Tooltip("How long to block that input (seconds).")]
        private float muteSeconds = 5f;

        [SerializeField]
        private bool forceStopIfActive = true;

        [Header("Slow Viewer Integration")]
        [SerializeField]
        private GameDataSO gameData;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            if (!vesselImpactor || !prismImpactee)
                return;

            var victimStatus = vesselImpactor.Vessel?.VesselStatus;
            var prism        = prismImpactee.Prism;

            if (!prism.prismProperties.IsDangerous)
                return;

            if (vesselImpactor.Vessel != null &&
                prism.Domain != vesselImpactor.Vessel.VesselStatus.Domain)
                return;

            if (victimStatus != null)
            {
                var handler = victimStatus.ActionHandler;
                if (handler != null)
                {
                    handler.MuteInput(inputToMute, muteSeconds);

                    if (forceStopIfActive)
                        handler.StopShipControllerActions(inputToMute);
                }
            }
            if (explosionDebuffAppliedEvent != null && vesselImpactor.Vessel != null)
            {
                var payload = new ExplosionDebuffPayload(vesselImpactor.Vessel, muteSeconds);
                explosionDebuffAppliedEvent.Raise(payload);
            }
            vesselSlowedByRhinoDangerPrismEvent?.Raise(vesselImpactor);
        }
    }
}
