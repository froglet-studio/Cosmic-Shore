using System;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SparrowDebuffByRhinoDangerPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/SparrowDebuffByRhinoDangerPrismEffectSO")]
    public sealed class SparrowDebuffByRhinoDangerPrismEffectSO : VesselPrismEffectSO
    {
        public static event Action<VesselImpactor> OnVesselSlowedByRhinoDangerPrism;
        public static event Action<IVessel, float> OnExplosionDebuffApplied;

        [Header("Debuff Settings")]
        [SerializeField, Tooltip("Which input to block on the victim (e.g., Boost).")]
        private InputEvents inputToMute = InputEvents.Button2Action;

        [SerializeField, Tooltip("How long to block that input (seconds).")]
        private float muteSeconds = 5f;

        [SerializeField, Tooltip("Force-stop once so continuous actions halt immediately.")]
        private bool forceStopIfActive = true;

        [Header("Slow Viewer Integration")]
        [SerializeField, Tooltip("Same GameDataSO used by SlowShipViewer (SlowedShipTransforms).")]
        private GameDataSO gameData;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            if (vesselImpactor == null || prismImpactee == null)
                return;

            var victimStatus = vesselImpactor.Vessel?.VesselStatus;
            var prism        = prismImpactee.Prism;
            

            if(!prism.prismProperties.IsDangerous)
                return;

            if (vesselImpactor.Vessel != null && prism.Domain != vesselImpactor.Vessel.VesselStatus.Domain)
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

            OnExplosionDebuffApplied?.Invoke(vesselImpactor.Vessel, muteSeconds);
            OnVesselSlowedByRhinoDangerPrism?.Invoke(vesselImpactor);
            
        }
    }
}