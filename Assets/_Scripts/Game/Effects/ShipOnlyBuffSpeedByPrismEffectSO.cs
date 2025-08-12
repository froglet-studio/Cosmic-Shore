using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipOnlyBuffSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipOnlyBuffSpeedByPrismEffectSO")]
    public class ShipOnlyBuffSpeedByPrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        [SerializeField]
        float _speedModifierDuration;
        
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_PrismImpactor prismImpactee)
        {
            var trailBlockProperties = prismImpactee.TrailBlock.TrailBlockProperties;
            
            if (trailBlockProperties.speedDebuffAmount > 1)
                shipImpactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}
