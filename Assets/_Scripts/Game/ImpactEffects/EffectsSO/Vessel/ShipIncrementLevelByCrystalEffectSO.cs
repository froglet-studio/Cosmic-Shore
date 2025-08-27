using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipIncrementLevelByCrystalEffectSO")]
    public class ShipIncrementLevelByCrystalEffectSO : ShipCrystalEffectSO
    {
        public override void Execute(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ResourceSystem.IncrementLevel(crystalImpactee.Crystal.crystalProperties.Element);
        }
    }
}
