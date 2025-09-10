using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselCrystalAdjustLevelOfEffect", menuName = "ScriptableObjects/Impact Effects/Crystal/VesselAdjustLevelByCrystalEffectSO")]
    public class VesselAdjustLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] int LevelAdjustment;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            vesselImpactor.Ship.ShipStatus.ResourceSystem.AdjustLevel(crystalImpactee.Crystal.crystalProperties.Element, LevelAdjustment);
        }
    }
}
