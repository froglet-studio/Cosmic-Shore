using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Squirrel's <b>Crystal Joust</b> - vessel impacts a LIVING lifeform's embedded
    /// crystal (its heart).
    ///
    /// BASE ability (no upgrade required): jousting an OPPOSING-domain lifeform while moving
    /// FASTER than it destroys the creature - it withers and dies through its normal death
    /// path (Fauna.Predated / LifeForm.Die), dropping its crystal exactly like starvation.
    /// An ACTIVE force: mass conserved, continuity honored, fauna spawn immunity respected.
    /// Rooted flora have CurrentSpeed 0, so they are trivially joustable; fast fauna must be
    /// overtaken. A slower vessel bounces off harmlessly.
    ///
    /// SPACE level-5 upgrade ('Shepherd'): jousting an OWN-domain lifeform NOURISHES it -
    /// LifeForm/Fauna.LevelUp() grows body + heart one level (max 5, the lifeform elemental
    /// contract). Below the unlock an ally joust does nothing.
    ///
    /// Per-impact live reads; the SO stays stateless.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselWitherLifeformByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Lifeform Crystal/VesselWitherLifeformByCrystalEffectSO")]
    public class VesselWitherLifeformByCrystalEffectSO : VesselLifeformCrystalEffectSO
    {
        [Tooltip("The element whose level-5 upgrade arms the ALLY branch - leveling up " +
                 "own-domain lifeforms (Space for the Squirrel). The opposing-domain kill is " +
                 "the BASE ability and is never gated.")]
        [SerializeField] Element allyUpgradeElement = Element.Space;

        [Tooltip("The vessel must be moving at least this much faster than the lifeform " +
                 "(world units/s) for the joust to land. Rooted flora sit at 0, so any moving " +
                 "vessel out-paces them; fast fauna must genuinely be overtaken.")]
        [SerializeField] float speedMargin = 1f;

        [Tooltip("Optional: raised with the jouster's player name on a successful joust " +
                 "(kill or ally level-up) - the Squirrel HUD listens on its impact icon channel.")]
        [SerializeField] ScriptableEventString onLifeformJousted;

        public override void Execute(VesselImpactor vesselImpactor, Crystal embeddedCrystal)
        {
            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            var lifeform = embeddedCrystal.EmbeddedIn;

            // The joust only lands when the vessel is genuinely moving faster than its target.
            if (status.Speed < lifeform.CurrentSpeed + speedMargin) return;

            // Own domain: Space-5 'Shepherd' - the joust nourishes the ally (never a kill).
            if (lifeform.Domain == status.Domain)
            {
                if (status.ElementalAbilityHandler?.IsUpgradeActive(allyUpgradeElement) != true) return;
                if (lifeform.LevelUp())
                {
                    CosmicShore.Utility.CSDebug.Log(
                        $"[CrystalJoust] Shepherd: {status.PlayerName} levelled an ally to L{lifeform.Level}.");
                    onLifeformJousted?.Raise(status.PlayerName);
                }
                return;
            }

            // Opposing domain: BASE ability - wither and die (idempotent; fauna spawn
            // immunity respected inside Predated).
            if (!lifeform.Jousted(status.PlayerName)) return;

            onLifeformJousted?.Raise(status.PlayerName);
            TakeHeart(vesselImpactor, embeddedCrystal);
        }

        /// <summary>
        /// The jouster TAKES the heart. The kill has already freed the crystal (a jousted death
        /// releases it at the strike, precisely so this can happen), and here it is awarded
        /// straight to the pilot who earned it instead of being left in the water. That is the
        /// whole shape of the joust: the heart leaves with the vessel and the creature's frame
        /// stays behind as a skeleton - the mirror of a starvation death, where nobody takes
        /// the heart and it becomes an ordinary pickup any vessel can collect once the wither
        /// exposes it (Docs/ECOSYSTEM.md §26).
        ///
        /// Degrades to the ordinary drop - the crystal simply sits there as a collectible -
        /// when the vessel carries no usable skimmer to award it through.
        /// </summary>
        static void TakeHeart(VesselImpactor vesselImpactor, Crystal crystal)
        {
            if (!crystal) return;

            var crystalImpactor = crystal.GetComponentInChildren<ElementalCrystalImpactor>(true);
            if (!crystalImpactor) return;

            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null) return;

            var skimmerImpactor = FindSkimmerImpactor(status.NearFieldSkimmer)
                                  ?? FindSkimmerImpactor(status.FarFieldSkimmer);
            if (!skimmerImpactor) return;

            crystalImpactor.CollectBy(skimmerImpactor);
        }

        /// <summary>The impactor a skimmer collects through, or null. Unity-null aware, so a
        /// destroyed/absent skimmer falls through to the next candidate rather than throwing.</summary>
        static SkimmerImpactor FindSkimmerImpactor(Skimmer skimmer)
        {
            if (!skimmer) return null;
            var impactor = skimmer.GetComponent<SkimmerImpactor>();
            if (impactor) return impactor;
            impactor = skimmer.GetComponentInChildren<SkimmerImpactor>(true);
            return impactor ? impactor : null;
        }
    }
}
