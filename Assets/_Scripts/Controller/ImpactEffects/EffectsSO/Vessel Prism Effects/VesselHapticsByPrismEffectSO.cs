using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The mistake feel: the vessel body slamming a prism fires one heavy, low-frequency thud
    /// (opposite of the bright skim). Wired into every vessel's prism-effect list. Local human pilot
    /// only. The gate in <see cref="HapticController.PlayPunish"/> spaces the thuds out and lets a
    /// thud interrupt an in-progress skim train (never the reverse).
    /// </summary>
    [CreateAssetMenu(fileName = "VesselHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselHapticsByPrismEffectSO")]
    public class VesselHapticsByPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var status = vesselImpactor.Vessel.VesselStatus;
            if (status == null || !status.IsLocalUser || status.AutoPilotEnabled) return;
            HapticController.PlayPunish(WeightOf(prismImpactee));
        }

        /// <summary>The hull-hit ladder (Docs/HAPTICS.md): danger or super-shielded hits hardest, shielded next.</summary>
        static PunishWeight WeightOf(PrismImpactor prismImpactee)
        {
            var props = prismImpactee && prismImpactee.Prism ? prismImpactee.Prism.prismProperties : null;
            if (props == null) return PunishWeight.Plain;
            if (props.IsDangerous || props.IsSuperShielded) return PunishWeight.Heavy;
            return props.IsShielded ? PunishWeight.Shielded : PunishWeight.Plain;
        }
    }
}
