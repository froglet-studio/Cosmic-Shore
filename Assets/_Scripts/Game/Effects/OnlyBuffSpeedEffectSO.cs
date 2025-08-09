using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OnlyBuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/OnlyBuffSpeedImpactEffectSO")]
    public class OnlyBuffSpeedEffectSO : ImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        /*public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (trailBlockProperties.speedDebuffAmount > 1)
                data.ThisShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
