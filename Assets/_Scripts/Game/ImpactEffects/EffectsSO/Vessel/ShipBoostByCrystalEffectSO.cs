using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBoostByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipBoostByCrystalEffectSO")]
    public class ShipBoostByCrystalEffectSO : ShipCrystalEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(ShipImpactor shipImpactor, CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(crystalImpactee.Crystal.crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}
