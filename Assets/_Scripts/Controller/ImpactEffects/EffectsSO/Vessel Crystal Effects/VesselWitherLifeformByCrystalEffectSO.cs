using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SPACE level-5 'Crystal Joust': jousting a LIVING lifeform's embedded crystal (its heart).
    /// OPPOSING-domain creature: withers and dies - routes through Fauna.Predated -> the sealed
    /// Fauna.Die, so the body withers from the extremities inward and drops its elemental
    /// crystal exactly like starvation (an ACTIVE force, mass conserved, continuity honored;
    /// predation immunity respected by Predated itself). OWN-domain creature: the joust NOURISHES
    /// it instead - Fauna.LevelUp() grows body + heart one level (max 5, the lifeform elemental
    /// contract). Gated per-impact on the jousting vessel's live Space upgrade - SO stays stateless.
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

            var fauna = embeddedCrystal.EmbeddedIn;

            // Own-domain creature: the joust nourishes it - level it up (body + heart grow a
            // level, capped at MaxLifeformLevel). Never a kill on your own pack.
            if (fauna.Domain == status.Domain)
            {
                if (fauna.LevelUp())
                    onLifeformJousted?.Raise(status.PlayerName);
                return;
            }

            // Opposing-domain creature: Predated is idempotent and respects the post-spawn
            // immunity window; true means it actually died (wither + crystal drop via sealed Die).
            if (fauna.Predated(status.PlayerName))
                onLifeformJousted?.Raise(status.PlayerName);
        }
    }
}
