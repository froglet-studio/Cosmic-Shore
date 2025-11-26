using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipIncrementLevelByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipIncrementLevelByCrystalEffectSO")]
    public class ShipIncrementLevelByCrystalEffectSO : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        protected override void ExecuteTyped(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.IncrementLevel(
                crystalImpactee.Crystal.crystalProperties.Element);
        }
    }
}