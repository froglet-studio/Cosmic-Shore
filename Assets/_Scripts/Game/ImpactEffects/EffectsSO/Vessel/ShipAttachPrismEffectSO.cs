using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipAttachPrismEffect", menuName = "ScriptableObjects/Impact Effects/ShipAttachPrismEffectSO")]
    public class ShipAttachPrismEffectSO : ImpactEffectSO<ShipImpactor, PrismImpactor>
    {
        protected override void ExecuteTyped(ShipImpactor shipImpactor, PrismImpactor prismImpactee)
        {
            IShipStatus shipStatus = shipImpactor.Ship.ShipStatus;
            TrailBlockProperties trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            if (trailBlockProperties == null)
            {
                Debug.LogError("ShipAttachPrismEffectSO called with null data or trailBlockProperties.");
                return;
            }

            var trailBlock = trailBlockProperties.trailBlock;

            if (trailBlock.Trail == null)
            {
                Debug.LogError("ShipAttachPrismEffectSO called with null data or Trail.");
                return;
            }

            shipStatus.Attached = true;
            shipStatus.AttachedTrailBlock = trailBlock;
        }
    }
}
