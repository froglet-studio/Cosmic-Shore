using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DebuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/DebuffSpeedImpactEffectSO")]
    public class DebuffSpeedEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        [SerializeField]
        float _speedModifierDuration;

        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}
