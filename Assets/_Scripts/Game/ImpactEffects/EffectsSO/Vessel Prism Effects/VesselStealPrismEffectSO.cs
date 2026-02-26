using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "VesselStealPrismEffect", 
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselStealPrismEffectSO")]
    public class VesselStealPrismEffectSO : VesselPrismEffectSO
    {
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;
            PrismEffectHelper.Steal(prismImpactee, status);
        }
    }
}
