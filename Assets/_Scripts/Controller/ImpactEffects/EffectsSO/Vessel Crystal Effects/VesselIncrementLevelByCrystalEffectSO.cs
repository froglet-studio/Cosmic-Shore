using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselIncrementLevelByCrystalEffectSO")]
    public class VesselIncrementLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.IncrementLevel(data.Element);
        }
    }
}
