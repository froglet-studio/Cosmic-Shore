using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's hunter-attack effect: a Rhino hunter's skimmer touching a human
    /// player's vessel applies the existing Rhino-style slow/mute debuff (same idiom
    /// as SparrowDebuffByRhinoDangerPrismEffectSO) and decrements the victim's
    /// RoundStats.Lives. Reaching 0 lives sets RoundStats.IsEliminated. Hunters are
    /// identified via FrictionHunterTag rather than Domain/VesselClassType, so this
    /// never triggers hunter-on-hunter or player-on-player.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselLifeLossByHunterSkimmer",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselLifeLossByHunterSkimmerEffectSO")]
    public class VesselLifeLossByHunterSkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        [Header("Debuff Settings")]
        [SerializeField] private InputEvents inputToMute = InputEvents.Button2Action;
        [SerializeField] private float muteSeconds = 3f;
        [SerializeField] private bool forceStopIfActive = true;

        [Header("Lives")]
        [Tooltip("Seconds of invulnerability after a hit, so a single hunter contact can't burn multiple lives.")]
        [SerializeField] private float invulnerabilitySeconds = 2f;

        [Header("Events")]
        [SerializeField, Tooltip("Raised (impactee context) when a hunter hit lands.")]
        private ScriptableEventVesselImpactor vesselHitByHunterEvent;

        [SerializeField, Tooltip("Raised (impactee context) when Lives reaches 0 and the victim is eliminated.")]
        private ScriptableEventVesselImpactor vesselEliminatedByHunterEvent;

        private static readonly Dictionary<VesselImpactor, float> _lastHitTimeByVictim = new();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null)
                return;

            if (impactee == null || impactee.Skimmer.VesselStatus.Vessel == null)
                return;

            // Only hunters cause life loss, and only against non-hunter (human/ally) vessels.
            if (impactor.Vessel.Transform.GetComponent<FrictionHunterTag>() == null)
                return;

            var victimVessel = impactee.Skimmer.VesselStatus.Vessel;
            if (victimVessel.Transform.GetComponent<FrictionHunterTag>() != null)
                return;

            var victimVesselImpactor = victimVessel.Transform.GetComponent<VesselImpactor>();
            if (victimVesselImpactor == null)
                return;

            var now = Time.time;
            if (_lastHitTimeByVictim.TryGetValue(victimVesselImpactor, out var lastHit) &&
                now - lastHit < invulnerabilitySeconds)
                return;

            var victimStats = gameData.RoundStatsList
                .FirstOrDefault(s => s.Name == victimVessel.VesselStatus.PlayerName);
            if (victimStats == null || victimStats.IsEliminated)
                return;

            _lastHitTimeByVictim[victimVesselImpactor] = now;

            var handler = victimVessel.VesselStatus.ActionHandler;
            if (handler != null)
            {
                handler.MuteInput(inputToMute, muteSeconds);
                if (forceStopIfActive)
                    handler.StopShipControllerActions(inputToMute);
            }

            victimStats.Lives = Mathf.Max(0, victimStats.Lives - 1);
            vesselHitByHunterEvent?.Raise(victimVesselImpactor);

            if (victimStats.Lives <= 0)
            {
                victimStats.IsEliminated = true;
                vesselEliminatedByHunterEvent?.Raise(victimVesselImpactor);
            }
        }
    }
}
