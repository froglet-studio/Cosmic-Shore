using CosmicShore.Game.Environment;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects
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
