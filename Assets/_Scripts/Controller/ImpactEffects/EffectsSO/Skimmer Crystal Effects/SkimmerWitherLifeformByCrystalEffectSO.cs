using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Scale Dust</b>, lifeform half — CHARGE. A living creature whose heart
    /// passes through the wings withers and dies through its normal death path
    /// (<c>Fauna.Predated</c> / <c>LifeForm.Die</c>), dropping its crystal exactly as starvation
    /// would. An ACTIVE force: mass conserved, continuity honoured, spawn immunity respected.
    ///
    /// <para><b>There is deliberately NO speed gate</b>, and that is the whole reason this is not
    /// the Squirrel's joust. <see cref="VesselWitherLifeformByCrystalEffectSO"/> requires the
    /// vessel to be moving FASTER than its target, because a joust is an overtake and the
    /// Squirrel's entire kit is speed. The Butterfly's kit is the opposite — it is the slowest
    /// hull in the fleet and it is meant to be — so an overtake requirement would mean this
    /// vessel could never kill anything that was not rooted. A butterfly does not ram; it drifts
    /// over something and the dust does the work.</para>
    ///
    /// <para><b>It rides the SKIMMER, not the hull</b>, which is what makes it an ability rather
    /// than a collision: the wings reach far past the body (SPACE sizes them), so what the
    /// Butterfly kills is what it passes OVER. The skimmer's new lifeform-crystal arm
    /// (<see cref="SkimmerLifeformCrystalEffectSO"/>) exists for exactly this and is empty on
    /// every other vessel.</para>
    ///
    /// <para><b>Wildlife is quarry whatever colour it wears</b> by default — the rule Wildlife
    /// Liberation records, for the reason it records it: fauna spawn in ONE colour, so borrowing a
    /// friendly-fire flag here would silently switch the ability off for a pilot who happened to
    /// share the swarm's colour, and the comeback system hands upgrades to whoever is LOSING.</para>
    ///
    /// <para>Per-impact live reads; the SO stays stateless and is shared by every Butterfly.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerWitherLifeformByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Lifeform Crystal/SkimmerWitherLifeformByCrystalEffectSO")]
    public class SkimmerWitherLifeformByCrystalEffectSO : SkimmerLifeformCrystalEffectSO
    {
        [Tooltip("On (default): only FAUNA are withered — creatures, not plants. A Butterfly " +
                 "erasing every flower it flew over would be at war with the food web it is " +
                 "supposed to be part of, and flora are what its own wake feeds.")]
        [SerializeField] bool faunaOnly = true;

        [Tooltip("Off (default): wildlife is QUARRY whatever colour it wears. On: creatures of " +
                 "the pilot's own domain are spared. Leave OFF unless a mode genuinely wants " +
                 "colour to protect a creature — see the class note on why borrowing a " +
                 "friendly-fire flag here is a trap.")]
        [SerializeField] bool sparesOwnDomain;

        [Tooltip("Optional: raised with the pilot's name on each creature withered — a HUD " +
                 "impact channel can listen on it.")]
        [SerializeField] ScriptableEventString onLifeformWithered;

        public override void Execute(SkimmerImpactor impactor, Crystal embeddedCrystal)
        {
            if (!impactor || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            var pilot = impactor.Skimmer != null ? impactor.Skimmer.VesselStatus : null;
            // An unattributed kill is an UNTRACKED one — Fauna.ReportKill drops nameless deaths
            // on purpose, so a mode scored on killing wildlife cannot have the wildlife killing
            // itself onto the scoreboard. Decline it here instead, where the target is chosen.
            if (pilot == null) return;

            var lifeform = embeddedCrystal.EmbeddedIn;
            if (lifeform == null) return;
            if (faunaOnly && lifeform is not Fauna) return;

            // ALREADY DYING — a corpse is not a target. IsEmbedded does NOT answer this: a
            // creature with a progressive wither re-homes its heart onto the cell at the top of
            // its death and leaves it embedded for the whole animation (Docs/ECOSYSTEM.md §26),
            // so a heart goes on matching for seconds after its owner died. Withering one again
            // is a second kill credit for one creature and frees the heart mid-wither.
            if (lifeform.IsDying) return;

            if (sparesOwnDomain && lifeform.Domain == pilot.Domain) return;

            if (lifeform.Jousted(pilot.PlayerName))
                onLifeformWithered?.Raise(pilot.PlayerName);
        }
    }
}
