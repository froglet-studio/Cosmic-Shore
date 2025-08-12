using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipIncrementLevelFromCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipIncrementLevelFromCrystalEffectSO")]
    public class ShipIncrementLevelFromCrystalEffectSO : ImpactEffectSO<R_ShipImpactor, R_CrystalImpactor>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.IncrementLevel(crystalImpactee.Crystal.crystalProperties.Element);
        }
    }
}
