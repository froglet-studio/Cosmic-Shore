using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeSpeedByPrismEffectSO")]
    public class VesselChangeSpeedByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] float speedModifierDuration = .03f;
        [SerializeField] float massScaling = .01f;
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;
            
            shipStatus.VesselTransformer.ModifyThrottle(Mathf.Min(trailBlockProperties.volume * massScaling, .2f), speedModifierDuration);
        }
    }
}
