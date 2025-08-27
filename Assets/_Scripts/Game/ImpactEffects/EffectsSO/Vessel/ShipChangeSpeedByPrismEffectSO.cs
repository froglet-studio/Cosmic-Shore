using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeSpeedByPrismEffect")]
    public class ShipChangeSpeedByPrismEffectSO : ShipPrismEffectSO
    {
        [SerializeField] private float speedModifierDuration;
        
        public override void Execute(ShipImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
        }
    }
}
