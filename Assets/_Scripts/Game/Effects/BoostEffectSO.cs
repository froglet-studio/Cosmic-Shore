using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BoostImpactEffect", menuName = "ScriptableObjects/Impact Effects/BoostImpactEffectSO")]
    public class BoostEffectSO : ImpactEffectSO, ICrystalImpactEffect
    {
        [SerializeField]
        float _speedModifierDuration;

        public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}
