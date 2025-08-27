using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ElementalCrystalAdjustLevelOfShipEffect", menuName = "ScriptableObjects/Impact Effects/Crystal/ElementalCrystalAdjustLevelOfShipEffectSO")]
    public class ElementalCrystalAdjustLevelOfShipEffectSO : ElementalCrystalShipEffectSO
    {
        [SerializeField] int LevelAdjustment;

        public override void Execute(ElementalCrystalImpactor impactor, ShipImpactor impactee)
        {
            impactee.Ship.ShipStatus.ResourceSystem.AdjustLevel(impactor.Crystal.crystalProperties.Element, LevelAdjustment);
        }
    }

}
