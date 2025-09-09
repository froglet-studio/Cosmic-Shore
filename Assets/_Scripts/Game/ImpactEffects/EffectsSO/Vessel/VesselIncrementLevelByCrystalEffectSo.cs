using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselIncrementLevelByCrystalEffectSo")]
    public class VesselIncrementLevelByCrystalEffectSo : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            vesselImpactor.Ship.ShipStatus.ResourceSystem.IncrementLevel(crystalImpactee.Crystal.crystalProperties.Element);
        }
    }
}
