using UnityEngine;
using Obvious.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselCollisionReporter", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselCollisionReporter")]
    public class VesselCollisionReporterSO : VesselCrystalEffectSO
    {
        [Header("Events")]
        [SerializeField] ScriptableEventString onSkimmerShipCollision;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;

            var playerName = vesselImpactor.Vessel.VesselStatus.PlayerName;
            CSDebug.Log($"<color=red>[Collision] {playerName} hit a crystal!</color>");
            onSkimmerShipCollision.Raise(playerName);
        }
    }
}