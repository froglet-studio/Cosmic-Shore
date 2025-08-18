using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ElementalCrystalAdjustLevelOfShipEffect", menuName = "ScriptableObjects/Impact Effects/Crystal/ElementalCrystalAdjustLevelOfShipEffectSO")]
    public class ElementalCrystalAdjustLevelOfShipEffectSO : ImpactEffectSO<ElementalCrystalImpactor, ShipImpactor>
    {
        [SerializeField] int LevelAdjustment;

        protected override void ExecuteTyped(ElementalCrystalImpactor impactor, ShipImpactor impactee)
        {
            impactee.Ship.ShipStatus.ResourceSystem.AdjustLevel(impactor.Crystal.crystalProperties.Element, LevelAdjustment);
        }
    }
}
