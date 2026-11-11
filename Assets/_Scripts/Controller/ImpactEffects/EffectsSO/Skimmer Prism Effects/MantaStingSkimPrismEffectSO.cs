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
    ///  • a skim that PAID energy fires the skim pulse haptic (<see cref="HapticController.PlaySkim"/>), local human
    ///    pilot only. Only a paying skim: the Manta's skimmer is 20-40 units across, so in dense prisms (the Toybox
    ///    cell, its own trail when it flies slower than the sphere) a pulse per prism ENTRY re-triggered the 70 ms clip
    ///    every 30 ms and held the pad at one steady level for as long as the Manta stayed there - minutes, with a full
    ///    bay meaning nothing at all. Tied to the energy, the pulses say "that paid" and stop when the bay is full.
    ///
    /// The energy and the plant gate on simulation authority inside the bay; the priming is a prism state and runs
    /// wherever this effect runs, like every other skim effect on prisms.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaStingSkimPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/MantaStingSkimPrismEffectSO")]
    public class MantaStingSkimPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Haptics")]
        [Tooltip("Skim pulse strength for a paying skim at the sphere's edge; a dead-centre skim plays at 1. " +
                 "Same floor as the fleet's SkimmerHapticsByPrismEffect (Docs/HAPTICS.md).")]
        [Range(0f, 1f)] [SerializeField] float hapticMinStrength = 0.35f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor?.Skimmer?.VesselStatus;
            if (status == null) return;
            if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) return;

            var prism = prismImpactee?.Prism;
            if (prism == null || prism.destroyed) return;

            if (bay.AddSkimCharge(prism) && status.Player != null && status.IsLocalUser && !status.AutoPilotEnabled)
                HapticController.PlaySkim(Mathf.Lerp(hapticMinStrength, 1f,
                    SkimmerHapticsByPrismEffectSO.Closeness01(impactor, prism)));
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
