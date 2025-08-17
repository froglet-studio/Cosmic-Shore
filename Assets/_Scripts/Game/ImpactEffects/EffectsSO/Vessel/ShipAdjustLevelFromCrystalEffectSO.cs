using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipAdjustLevelFromCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipAdjustLevelFromCrystalEffectSO")]
    public class ShipAdjustLevelFromCrystalEffectSO : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        [SerializeField] int LevelAdjustment;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.AdjustLevel(crystalImpactee.Crystal.crystalProperties.Element, LevelAdjustment);
        }
    }
}
