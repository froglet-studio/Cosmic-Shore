using CosmicShore.Core;
using UnityEngine;

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
                Debug.LogError("[VesselCollisionReporter] StatsManager Instance is NULL! Collision not recorded.");
                return;
            }
            Debug.Log($"<color=red>[Collision] {vesselImpactor.Vessel.VesselStatus.PlayerName} hit a crystal!</color>");
            StatsManager.Instance.ExecuteSkimmerShipCollision(vesselImpactor.Vessel.VesselStatus.PlayerName);
        }
    }
}