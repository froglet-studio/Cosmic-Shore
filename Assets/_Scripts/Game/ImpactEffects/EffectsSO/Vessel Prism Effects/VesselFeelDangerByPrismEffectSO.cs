using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselFeelDangerByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselFeelDangerByPrismEffectSO")]
    public class VesselFeelDangerByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float duration;
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != shipStatus.Team)
            {
                shipStatus.VesselTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, duration);
            }
        }
    }
}
