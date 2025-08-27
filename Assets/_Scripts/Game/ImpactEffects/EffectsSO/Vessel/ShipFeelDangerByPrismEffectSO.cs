using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipFeelDangerByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipFeelDangerByPrismEffectSO")]
    public class ShipFeelDangerByPrismEffectSO : ShipPrismEffectSO
    {
        [SerializeField] private float duration;
        
        public override void Execute(ShipImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != shipStatus.Team)
            {
                shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, duration);
            }
        }
    }
}
