using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselIncrementLevelByCrystalEffectSO")]
    public class VesselIncrementLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.IncrementLevel(crystalImpactee.Crystal.crystalProperties.Element);
        }
    }
}
