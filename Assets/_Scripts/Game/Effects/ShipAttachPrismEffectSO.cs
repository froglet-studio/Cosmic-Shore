using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AttachImpactEffect", menuName = "ScriptableObjects/Impact Effects/AttachImpactEffectSO")]
    public class ShipAttachPrismEffectSO : ImpactEffectSO<R_ShipImpactor, R_PrismImpactor>
    {
        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_PrismImpactor prismImpactee)
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
