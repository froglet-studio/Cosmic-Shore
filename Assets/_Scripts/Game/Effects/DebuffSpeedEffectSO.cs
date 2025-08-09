using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DebuffSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/DebuffSpeedImpactEffectSO")]
    public class DebuffSpeedEffectSO : ImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        /*public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
