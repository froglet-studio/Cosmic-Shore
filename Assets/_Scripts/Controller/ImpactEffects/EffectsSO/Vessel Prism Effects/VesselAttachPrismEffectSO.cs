using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselAttachPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselAttachPrismEffectSO")]
    public class VesselAttachPrismEffectSO : VesselPrismEffectSO
    {
        // No "arm the guns on attach" flag. One existed (armGunsOnAttach) and wrote
        // VesselStatus.GunsActive, which no gun, executor or AI decision reads, so it restored
        // nothing; it was dropped with its claim (URCHIN_BACKLOG U6). The Urchin fires from a
        // ride because its spike executor fires whenever its trigger is held, attached or not.

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
            if (!trailBlock) return;

            // No Trail gate here - deliberately. A prism without a container is still a
            // prismscape (a flora shell, a lone block: Surface / Singleton), and the RIDE
            // routing (GunVesselTransformer.TryBeginRide via PrismscapeTopology) is what
            // decides how - or whether - it is ridden. The old null-Trail refusal predates
            // the dimension ladder and silently made every container-less prism in the game
            // unattachable, while logging an error for what is a perfectly ordinary contact.
            vesselStatus.IsAttached = true;
            vesselStatus.AttachedPrism = trailBlock;
        }
    }
}
