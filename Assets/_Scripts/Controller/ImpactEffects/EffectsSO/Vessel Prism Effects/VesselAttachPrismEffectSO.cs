using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselAttachPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselAttachPrismEffectSO")]
    public class VesselAttachPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            IVesselStatus vesselStatus = vesselImpactor.Vessel.VesselStatus;
            PrismProperties prismProperties = prismImpactee.Prism.prismProperties;
            
            if (prismProperties == null)
            {
                CSDebug.LogError("VesselAttachPrismEffectSO called with null data or prismProperties.");
                return;
            }

            var trailBlock = prismProperties.prism;

            if (trailBlock.Trail == null)
            {
                CSDebug.LogError("VesselAttachPrismEffectSO called with null data or Trail.");
                return;
            }

            vesselStatus.IsAttached = true;
            vesselStatus.AttachedPrism = trailBlock;
        }
    }
}
