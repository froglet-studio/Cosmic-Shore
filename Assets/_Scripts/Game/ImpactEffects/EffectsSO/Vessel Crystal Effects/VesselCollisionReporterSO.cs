using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects
{
    [CreateAssetMenu(fileName = "VesselCollisionReporter", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselCollisionReporter")]
    public class VesselCollisionReporterSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;

            if (StatsManager.Instance == null)
            {
                CSDebug.LogError("[VesselCollisionReporter] StatsManager Instance is NULL! Collision not recorded.");
                return;
            }
            CSDebug.Log($"<color=red>[Collision] {vesselImpactor.Vessel.VesselStatus.PlayerName} hit a crystal!</color>");
            StatsManager.Instance.ExecuteSkimmerShipCollision(vesselImpactor.Vessel.VesselStatus.PlayerName);
        }
    }
}