using CosmicShore.Game.Ship;
using UnityEngine;
using CosmicShore.Models.Enums;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "VesselFeelDangerByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselFeelDangerByPrismEffectSO")]
    public class VesselFeelDangerByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float duration;
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;
            
            if (trailBlockProperties.IsDangerous && trailBlockProperties.prism.Domain != shipStatus.Domain)
            {
                shipStatus.VesselTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, duration);
            }
        }
    }
}
