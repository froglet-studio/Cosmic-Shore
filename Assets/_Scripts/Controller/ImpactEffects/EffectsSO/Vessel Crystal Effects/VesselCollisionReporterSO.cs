using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
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