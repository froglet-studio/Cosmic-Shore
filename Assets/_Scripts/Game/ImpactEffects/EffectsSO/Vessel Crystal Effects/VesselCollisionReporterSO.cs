using UnityEngine;
using CosmicShore.Game.Environment;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.ImpactEffects
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