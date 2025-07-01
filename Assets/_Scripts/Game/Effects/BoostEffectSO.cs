using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BoostImpactEffect", menuName = "ScriptableObjects/Impact Effects/BoostImpactEffectSO")]
    public class BoostEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ShipTransformer.ModifyThrottle(context.CrystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}
