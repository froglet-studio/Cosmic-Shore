using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselDamageBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselDamageBySkimmerEffectSO")]
    public sealed class VesselDamageBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Victim Input Suppression")]
        [SerializeField, Tooltip("Which input to block on the victim (e.g., Fire or RightStick).")]
        private InputEvents inputToMute = InputEvents.RightStickAction;

        [SerializeField, Tooltip("How long to block that input (seconds).")]
        private float muteSeconds = 5f;

        [SerializeField, Tooltip("Force-stop once so continuous actions halt immediately.")]
        private bool forceStopIfActive = true;

        public override void Execute(VesselImpactor vesselImpactor, SkimmerImpactor skimmerImpactee)
        {
            var victimStatus = vesselImpactor?.Vessel?.VesselStatus;
            var attackerStatus =  skimmerImpactee.Skimmer.VesselStatus ;
            if (attackerStatus == null) return;
            if (attackerStatus.VesselType != VesselClassType.Rhino) return;

            if (victimStatus == null) return;
            var handler = victimStatus.ActionHandler;
            handler.MuteInput(inputToMute, muteSeconds);

            if (forceStopIfActive)
                handler.StopShipControllerActions(inputToMute);
        }
    }
}