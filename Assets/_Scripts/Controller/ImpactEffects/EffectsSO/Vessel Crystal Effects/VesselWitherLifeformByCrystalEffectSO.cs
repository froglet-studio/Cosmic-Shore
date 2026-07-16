using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SPACE level-5 'Crystal Joust': jousting a LIVING lifeform's embedded crystal (its heart)
    /// withers the creature. Routes through Fauna.Predated -> the sealed Fauna.Die, so the body
    /// withers from the extremities inward and drops its elemental crystal exactly like
    /// starvation - an ACTIVE force, mass conserved, continuity honored. Gated per-impact on
    /// the jousting vessel's live Space upgrade (IsUpgradeActive) - the SO stays stateless.
    /// Predation immunity (post-spawn grace) is respected by Predated itself.
    /// </summary>
    [CreateAssetMenu(fileName = "VesselWitherLifeformByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Lifeform Crystal/VesselWitherLifeformByCrystalEffectSO")]
    public class VesselWitherLifeformByCrystalEffectSO : VesselLifeformCrystalEffectSO
    {
        [Tooltip("The element whose level-5 upgrade arms this joust (Space for the Squirrel).")]
        [SerializeField] Element gatingElement = Element.Space;

        [Tooltip("Optional: raised with the jouster's player name on a successful lifeform joust " +
                 "(the Squirrel HUD listens on its shared joust/crystal impact icon channel).")]
        [SerializeField] ScriptableEventString onLifeformJousted;

        public override void Execute(VesselImpactor vesselImpactor, Crystal embeddedCrystal)
        {
            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            if (status.ElementalAbilityHandler?.IsUpgradeActive(gatingElement) != true) return;

            // Predated is idempotent and respects the post-spawn immunity window; true means
            // the creature actually died to this joust (wither + crystal drop via sealed Die).
            if (embeddedCrystal.EmbeddedIn.Predated(status.PlayerName))
                onLifeformJousted?.Raise(status.PlayerName);
        }
    }
}
