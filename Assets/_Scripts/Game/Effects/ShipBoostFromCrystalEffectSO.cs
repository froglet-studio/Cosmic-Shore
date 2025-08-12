using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBoostFromCrystalEffect", menuName = "ScriptableObjects/Impact Effects/ShipBoostFromCrystalEffectSO")]
    public class ShipBoostFromCrystalEffectSO : ImpactEffectSO<R_ShipImpactor, R_CrystalImpactor>
    {
        [SerializeField]
        float _speedModifierDuration;

        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_CrystalImpactor crystalImpactee)
        {
            shipImpactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(crystalImpactee.Crystal.crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}
