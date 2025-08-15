using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipFeelDangerPrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipFeelDangerPrismEffectSO")]
    public class ShipFeelDangerPrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        protected override void ExecuteTyped(R_ShipImpactor impactor, R_PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != shipStatus.Team)
            {
                shipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
            }
        }
    }
}
