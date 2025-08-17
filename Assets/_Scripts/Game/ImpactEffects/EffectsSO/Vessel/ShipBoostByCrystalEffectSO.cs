using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBoostByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipBoostByCrystalEffectSO")]
    public class ShipBoostByCrystalEffectSO : ImpactEffectSO<ShipImpactor, CrystalImpactor>
    {
        [SerializeField]
        float _speedModifierDuration;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(crystalImpactee.Crystal.crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}
