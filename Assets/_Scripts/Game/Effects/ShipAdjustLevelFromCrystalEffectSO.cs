using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipAdjustLevelFromCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipAdjustLevelFromCrystalEffectSO")]
    public class ShipAdjustLevelFromCrystalEffectSO : ImpactEffectSO<R_ShipImpactor, R_CrystalImpactor>
    {
        [SerializeField] int LevelAdjustment;

        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.AdjustLevel(crystalImpactee.Crystal.crystalProperties.Element, LevelAdjustment);
        }
    }
}
