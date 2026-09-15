using CosmicShore.Data;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A blast that KILLS CREATURES — the Squirrel's Crystal Joust reached by an explosion
    /// instead of by a hull.
    ///
    /// <para>It runs the identical death: <see cref="ILifeFormEntity.Jousted"/> stamps
    /// <see cref="LifeformDeathStyle.Jousted"/> and routes through the sealed
    /// <c>Fauna.Die</c>/<c>LifeForm.Die</c>, so the creature does NOT detonate — its heart is
    /// freed at the strike, its soft tissue unravels FROM THE HEART OUTWARD around the hole, and
    /// its body prisms are left standing as a skeleton the food web then grazes
    /// (Docs/ECOSYSTEM.md §26). Mass is conserved, continuity is honoured, spawn immunity is
    /// respected, and the kill is attributed to the pilot who fired the blast so
    /// <c>ScoringMetric.LifeformsKilled</c> credits it.</para>
    ///
    /// <para><b>The one deliberate difference from the Squirrel's joust: nobody takes the
    /// heart.</b> A jousting vessel reaches in and collects it
    /// (<c>VesselWitherLifeformByCrystalEffectSO.TakeHeart</c>); a blast is standing off at range
    /// and does not, so the heart drops as an ordinary pickup any vessel can collect — the same
    /// end state a starvation death reaches. That is a balance decision as much as a fictional
    /// one: a rocket that kills a dozen creatures would otherwise hand its pilot a dozen
    /// elemental crystals in one frame.</para>
    ///
    /// <para><b>There is no speed contest.</b> The vessel joust requires the pilot to be moving
    /// faster than its target (you have to catch it); a blast has no such notion and simply
    /// reaches everything inside it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "ExplosionWitherLifeformByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Lifeform Crystal/ExplosionWitherLifeformByCrystalEffectSO")]
    public class ExplosionWitherLifeformByCrystalEffectSO : ExplosionLifeformCrystalEffectSO
    {
        [Tooltip("On (default): only FAUNA are killed — creatures, not plants. Flora are the " +
                 "food web's standing mass and the blast's own prism half already eats them; " +
                 "killing a plant outright through its heart would delete a whole grown " +
                 "structure per rocket. Off: any lifeform in the blast dies.")]
        [SerializeField] bool faunaOnly = true;

        [Tooltip("Off (default): wildlife is QUARRY whatever colour it wears. On: creatures of " +
                 "the shooter's own domain are spared.\n\n" +
                 "This is deliberately the effect's OWN decision and not the blast's friendly-" +
                 "fire flag, because the two answer different questions. The blast's flag is a " +
                 "snapshot of an upgrade about MASS (the Sparrow's CHARGE-5 'Domain-Safe " +
                 "Skybursts' — do not blow up your own trail). Borrowing it here made an upgrade " +
                 "about prisms silently switch off your WILDLIFE kills: fauna spawn in exactly " +
                 "ONE colour, the cell's controlling domain, so in Wildlife Liberation — the " +
                 "only mode scored on LifeformsKilled — a pilot sharing that colour could not " +
                 "kill a creature with this blast at all, and the comeback system hands element " +
                 "levels to whoever is LOSING, so falling behind bought a nerf to the scoring " +
                 "weapon. It also disagreed with the mode's primary kill: shooting a creature's " +
                 "body prisms kills it regardless of colour, and always has.")]
        [SerializeField] bool sparesOwnDomain;

        [Tooltip("Optional: raised with the killing pilot's name on each creature killed — the " +
                 "same channel the Squirrel's joust reports on, for HUD feedback.")]
        [SerializeField] ScriptableEventString onLifeformJousted;

        public override void Execute(ExplosionImpactor impactor, Crystal embeddedCrystal)
        {
            if (!impactor || embeddedCrystal == null || !embeddedCrystal.IsEmbedded) return;

            // An ANONYMOUS blast has no pilot to credit the kill to. Fauna.ReportKill drops
            // unattributed deaths on purpose (a mode whose objective is killing wildlife cannot
            // have the wildlife killing itself onto the scoreboard), so a nameless kill here
            // would be an untracked one — decline it outright instead.
            var shooter = impactor.SourceVessel?.VesselStatus;
            if (shooter == null) return;

            var lifeform = embeddedCrystal.EmbeddedIn;
            if (lifeform == null) return;
            if (faunaOnly && lifeform is not Fauna) return;

            // ALREADY DYING - a corpse is not a target. IsEmbedded above does NOT answer this:
            // a creature with a progressive wither re-homes its heart onto the cell at the top
            // of its death and leaves it embedded, uncollectable, for the whole animation
            // (Docs/ECOSYSTEM.md §26), so a heart keeps matching for seconds after its owner
            // died. Jousting one re-runs the sealed death - a second LifeformsKilled credit for
            // one creature, and the heart freed while the wither is still eating inward.
            // Fauna.Predated declines a corpse at the source; this declines it before the
            // dispatch, so the reason is visible where the target is chosen.
            if (lifeform.IsDying) return;

            // Own-domain wildlife: this effect's OWN authored choice, NOT the blast's
            // friendly-fire flag (see the tooltip). Default is that colour buys a creature
            // nothing, which is what shooting its body prisms has always done.
            if (sparesOwnDomain && lifeform.Domain == shooter.Domain) return;

            if (lifeform.Jousted(shooter.PlayerName))
                onLifeformJousted?.Raise(shooter.PlayerName);
        }
    }
}
