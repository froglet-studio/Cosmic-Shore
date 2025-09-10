using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeSpeedByPrismEffectSO")]
    public class VesselChangeSpeedByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float speedModifierDuration;
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, speedModifierDuration);
        }
    }
}
