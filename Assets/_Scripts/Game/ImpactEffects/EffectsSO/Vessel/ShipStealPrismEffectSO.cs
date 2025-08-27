using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipStealPrismEffect", 
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipStealPrismEffectSO")]
    public class ShipStealPrismEffectSO : ShipPrismEffectSO
    {
        
        public override void Execute(ShipImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Ship.ShipStatus;
            Steal(prismImpactee, status);
        }
    }
}
