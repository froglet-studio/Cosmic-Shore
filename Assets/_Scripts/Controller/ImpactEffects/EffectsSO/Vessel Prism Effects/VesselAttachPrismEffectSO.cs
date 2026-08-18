using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselAttachPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselAttachPrismEffectSO")]
    public class VesselAttachPrismEffectSO : VesselPrismEffectSO
    {
        [Tooltip("Arm the vessel's guns on attach. On for the Urchin, whose ride exists to " +
                 "carry it into firing range of enemy mass.")]
        [SerializeField] bool armGunsOnAttach = true;

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

            // Riding arms the guns. The Urchin fires its spike volleys FROM the trail it is
            // riding - that is the whole loop, ride to reach enemy mass, then convert it - so
            // an attach that leaves the guns cold makes the ride a movement option with no
            // payoff. Lost in the vessel-layer port; restored here rather than in the
            // transformer so it lands on whatever attaches, not just the Urchin.
            if (armGunsOnAttach) vesselStatus.GunsActive = true;
        }
    }
}
