using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OnlyBuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/OnlyBuffSpeedImpactEffectSO")]
    public class OnlyBuffSpeedEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(ImpactContext context)
        {
            if (context.TrailBlockProperties.speedDebuffAmount > 1)
                context.ShipStatus.ShipTransformer.ModifyThrottle(context.TrailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}
