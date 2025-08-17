using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipChangeSpeedByPrismEffect")]
    public class ShipChangeSpeedByPrismEffectSO : ImpactEffectSO<ShipImpactor, PrismImpactor>
    {
        [SerializeField] private float speedModifierDuration;
        
        protected override void ExecuteTyped(ShipImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
        }
    }
}
