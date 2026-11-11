using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's SKIM (MANTA_STING_KABLOOM.md §2-3a) - one skimmer-prism effect because to the impact system all of it
    /// is the same contact, the Manta's skimmer touching a prism:
    ///  • every skim tick pays ENERGY (<see cref="MantaStingActionExecutor.AddSkimCharge"/>, per-prism throttled) - the
    ///    Space card's gauge, spent one bomb at a time to plant;
    ///  • an OPPOSING domain's prism is primed EXPLOSIVE (<see cref="Prism.MakeExplosive"/>): when it dies the prisms
    ///    beside it in its prismscape die too, and an explosive neighbour carries the chain on. A shielded prism keeps
    ///    its armour and is not primed - until SPACE 5, where the skim pops the shield and primes it anyway;
    ///  • a skimmed prism that is a LIVING CREATURE'S BODY plants a bomb on the creature (graze to plant);
    ///  • no haptic: skimming is told by sound and the HUD only (Caleb, 2026-11-11). The Manta's pad speaks on a hull
    ///    collision (the punish thud) and when a bomb is planted (<see cref="HapticController.PlayPlant"/>).
    ///
    /// The energy and the plant gate on simulation authority inside the bay; the priming is a prism state and runs
    /// wherever this effect runs, like every other skim effect on prisms.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaStingSkimPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/MantaStingSkimPrismEffectSO")]
    public class MantaStingSkimPrismEffectSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor?.Skimmer?.VesselStatus;
            if (status == null) return;
            if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) return;

            var prism = prismImpactee?.Prism;
            if (prism == null || prism.destroyed) return;

            bay.AddSkimCharge(prism);
            TryPrimeExplosive(bay, status, prism);

            if (prism is HealthPrism healthPrism)
            {
                var fauna = healthPrism.ResolveOwnerFauna();
                if (fauna != null)
                    bay.TryPlantOnFauna(fauna);
            }
        }

        static void TryPrimeExplosive(MantaStingActionExecutor bay, IVesselStatus status, Prism prism)
        {
            var cfg = bay.Config;
            if (!cfg || !cfg.SkimPrimesExplosive || prism.IsExplosive) return;

            // Opposing domains only: never the Manta's own side, never the neutral environment.
            var domain = prism.Domain;
            if (domain == status.Domain || domain == Domains.Blue) return;

            var props = prism.prismProperties;
            if (props == null) return;
            if (props.IsShielded || props.IsSuperShielded)
            {
                // SPACE 5: the skim pops the armour, then primes what is under it.
                var handler = status.ElementalAbilityHandler;
                if (!handler || !handler.IsUpgradeActive(Element.Space)) return;
                prism.DeactivateShields();
            }

            prism.MakeExplosive(status.PlayerName, cfg.ExplosiveLift, cfg.ExplosiveLiftFadeSeconds);
        }
    }
}
