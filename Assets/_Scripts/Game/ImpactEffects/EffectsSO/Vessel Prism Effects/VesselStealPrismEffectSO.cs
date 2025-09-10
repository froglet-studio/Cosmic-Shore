using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselStealPrismEffect", 
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselStealPrismEffectSO")]
    public class VesselStealPrismEffectSO : VesselPrismEffectSO
    {
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Ship.ShipStatus;
            PrismEffectHelper.Steal(prismImpactee, status);
        }
    }
}
