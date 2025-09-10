using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselAttachPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselAttachPrismEffectSO")]
    public class VesselAttachPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            IShipStatus shipStatus = vesselImpactor.Ship.ShipStatus;
            TrailBlockProperties trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            if (trailBlockProperties == null)
            {
                Debug.LogError("VesselAttachPrismEffectSO called with null data or trailBlockProperties.");
                return;
            }

            var trailBlock = trailBlockProperties.trailBlock;

            if (trailBlock.Trail == null)
            {
                Debug.LogError("VesselAttachPrismEffectSO called with null data or Trail.");
                return;
            }

            shipStatus.Attached = true;
            shipStatus.AttachedTrailBlock = trailBlock;
        }
    }
}
