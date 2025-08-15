using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipDebuffSpeedPrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipDebuffSpeedPrismEffectSO")]
    public class ShipDebuffSpeedPrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        [SerializeField] private float speedModifierDuration;
        
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
        }
    }
}
