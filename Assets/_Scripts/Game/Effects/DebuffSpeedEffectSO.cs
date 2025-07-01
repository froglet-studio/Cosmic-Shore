using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DebuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/DebuffSpeedImpactEffectSO")]
    public class DebuffSpeedEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ShipTransformer.ModifyThrottle(context.TrailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}
