using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OnlyBuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/OnlyBuffSpeedImpactEffectSO")]
    public class OnlyBuffSpeedEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        [SerializeField]
        float _speedModifierDuration;

        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (trailBlockProperties.speedDebuffAmount > 1)
                data.ThisShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}
