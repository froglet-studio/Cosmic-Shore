using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game
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