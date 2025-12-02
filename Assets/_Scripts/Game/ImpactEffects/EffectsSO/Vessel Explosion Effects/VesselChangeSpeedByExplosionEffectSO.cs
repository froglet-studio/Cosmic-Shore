using System;
using CosmicShore.Core;
using CosmicShore.Models.Enums;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeSpeedByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselChangeSpeedByExplosionEffectSO")]
    public class VesselChangeSpeedByExplosionEffectSO : VesselExplosionEffectSO
    {
        /// <summary>
        /// attacker-independent visual debuff: who was hit & for how long
        /// </summary>
        public static event Action<IVessel, float> OnExplosionDebuffApplied;
        public static event Action<VesselImpactor> OnVesselSlowedByExplosion;

        [Header("Victim Input Suppression")]
        [SerializeField] private InputEvents inputToMute = InputEvents.RightStickAction;
        [SerializeField] private float muteSeconds = 3f;
        [SerializeField] private bool forceStopIfActive = true;

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
            OnExplosionDebuffApplied?.Invoke(victimVessel, muteSeconds);
            OnVesselSlowedByExplosion?.Invoke(impactor);
        }
    }
}