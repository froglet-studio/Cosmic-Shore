using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ReduceSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/ReduceSpeedImpactEffectSO")]
    public class ReduceSpeedEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ShipTransformer.ModifyThrottle(.1f, 3);  // TODO: Magic numbers
        }
    }
}
