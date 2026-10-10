using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Scale Dust</b>, pilot half — CHARGE. An opposing pilot caught inside the
    /// dust has petals STOLEN off every element (<see cref="ElementalTransferForm.Steal"/>, the
    /// contact form), through <see cref="IContactPetalTake.TakeFrom"/>.
    ///
    /// <para><b>Since 2026-10-10 the take runs only through the reporter.</b> A scored hit and a
    /// petal theft are one event, so the container's <see cref="VesselCombatHitBySkimmerEffectSO"/>
    /// admits and scores the contact and calls <see cref="TakeFrom"/>. Before, this effect applied
    /// its own temporary, decaying debuff on its own cooldown: a dusting scored, the debuff decayed
    /// back in four seconds, and no petal changed hands. <c>Execute</c> now only plays the bite's
    /// sound.</para>
    ///
    /// <para>Per the design philosophy, elementals are the single system that governs all buffing
    /// and debuffing — a vessel that wants to weaken a pilot reaches for that fundamental rather
    /// than inventing a per-hull status. It carries no gameplay consequence of its own beyond the
    /// drain: the SCORING of the contact is
    /// <see cref="VesselCombatHitBySkimmerEffectSO"/> sitting in the same container, which is the
    /// separation that lets a mode price this hit without the Butterfly knowing which mode it is
    /// in.</para>
    ///
    /// <para><b>Classed <see cref="ElementalDebuffSources.VesselContact"/></b>, not DangerPrism: a
    /// ward earned against the ARENA must not cancel a weapon another pilot aimed. That is the
    /// scoping rule the Dolphin's Drift Ward paid for, applied on the way in rather than after
    /// the fact.</para>
    ///
    /// <para><b>Own-domain contact is declined here</b>, unlike the explosion version, which can
    /// rely on <c>ExplosionImpactor.AcceptImpactee</c> having already dropped a teammate. A
    /// skimmer has no such gate for vessels — <c>Skimmer.affectSelf</c> is a DOMAIN compare
    /// evaluated AFTER the effect loop and only gates skim bookkeeping — so without this test a
    /// Butterfly would debuff the teammate flying beside it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalDebuffBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselElementalDebuffBySkimmerEffectSO")]
    public class VesselElementalDebuffBySkimmerEffectSO : VesselSkimmerEffectsSO, IContactPetalTake
    {
        [Header("Debuff Settings")]
        [Tooltip("Signed level change applied to every element the dust drains (negative = " +
                 "debuff). DERIVED, not authored: a hit's bite tracks the price Broadside puts " +
                 "on its verb — total drain = points x (2.0/12), spread over the elements " +
                 "touched. The Butterfly's dust is a Strike-class contact (8 points), so the " +
                 "total is 1.3333 and over four elements that is -0.333333 each. See " +
                 "Tools/Build/author_combat_debuff_magnitudes.py.")]
        [SerializeField] float debuffMagnitude = -0.3333333f;

        [Tooltip("UNUSED at runtime since the dust became a steal (2026-10-10): a transfer is " +
                 "permanent and has nothing to decay. Kept because the authoring tool reads it " +
                 "to price sustained pressure.")]
        [SerializeField] float debuffDuration = 4f;

        [Tooltip("Which elements the dust drains. All four: the dust is a whole-pilot effect, " +
                 "not a targeted one, and a butterfly does not pick which of your systems it " +
                 "settles on.")]
        [SerializeField] Element[] elements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Header("Bite by element")]
        [Tooltip("A multiplier on the drain, scaled by the ATTACKING pilot's level in its element " +
                 "(the Butterfly: CHARGE — Charge is threat). Disabled = x1, which is every other " +
                 "adopter. Read through the pilot's REPLICATED integer level " +
                 "(R_VesselElementalAbilityHandler.ReplicatedLevel): the drain lands on the " +
                 "victim's OWN machine, which has no other way to know how charged the attacker " +
                 "is — element levels never replicate, so a local read there would use the " +
                 "attacker replica's level and the scaling would be inert in any real match.")]
        [SerializeField] ElementalFloat biteScale = new(1f);

        [Header("Upgrade")]
        [Tooltip("The element whose level-5 upgrade deepens the bite (Charge, for the " +
                 "Butterfly's 'Monarch'). None disables the upgrade branch entirely, which is " +
                 "what any other vessel adopting this effect should author.")]
        [SerializeField] Element upgradeElement = Element.Charge;

        [Tooltip("What the drain magnitude is multiplied by while that upgrade is live.")]
        [SerializeField, Min(1f)] float upgradeBiteMultiplier = 2f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between dust BITE SOUNDS on the same vessel. The take itself " +
                 "is rate-limited by the reporter's latch window, which is what the score uses.")]
        [SerializeField, Min(0f)] float cooldown = 1f;

        // Per-vessel anti-spam, keyed on the victim's ResourceSystem: one VesselEffectCooldowns
        // per effect type (the debuff and overtake effects share the shape, not the table). The
        // table prunes destroyed vessels on first sight of a new one, so it cannot grow by dead
        // vessels across matches.
        static readonly VesselEffectCooldowns _cooldowns = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cooldowns.Clear();

        /// <summary>
        /// NOTE the argument order, shared with every other skimmer effect and inverted from what
        /// the names suggest: <paramref name="impactor"/> is the vessel that was SWEPT (the
        /// victim) and <paramref name="impactee"/> is the skimmer doing the sweeping.
        /// </summary>
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (!impactor || impactor.Vessel == null) return;

            var victim = impactor.Vessel.VesselStatus;
            var pilot = impactee != null && impactee.Skimmer != null
                ? impactee.Skimmer.VesselStatus : null;
            if (victim == null || pilot == null) return;

            // A teammate sweep is not an attack — see the class note on why the skimmer cannot
            // rely on an upstream domain gate the way the explosion path can.
            if (victim.Domain == pilot.Domain) return;

            var rs = victim.ResourceSystem;
            if (rs == null) return;

            float now = Time.time;
            if (!_cooldowns.TryBegin(rs, now, cooldown))
                return;

            // The PETALS are not taken here: see TakeFrom, which the reporter calls for an
            // admitted hit. This keeps only the sound every peer should hear.

            // The bite's voice. Its slot lives on the Butterfly's dust capsule
            // (ButterflyDustField), beside the SkimmerImpactor doing the sweeping; any other
            // adopter of this effect has no such component and stays silent here.
            if (impactee.TryGetComponent(out ButterflyDustField dust))
                dust.PlayScaleDustBite(impactor.transform.position);
        }

        /// <summary>
        /// Steal this dusting's bite off <paramref name="victim"/>. Per-hit snapshot of the
        /// Charge scale and the level-5 upgrade, both read through REPLICATED state so the size of
        /// a take never depends on which copy of the pilot a machine is looking at. Called only by
        /// <see cref="VesselCombatHitBySkimmerEffectSO"/>, on the pilot's owner, for an admitted
        /// hit; the victim's owner settles it.
        /// </summary>
        public void TakeFrom(IVesselStatus victim, IVesselStatus attacker, VesselImpactor impactor,
                             SkimmerImpactor impactee)
        {
            if (victim == null || attacker == null || elements == null) return;

            float magnitude = debuffMagnitude * BiteScale(attacker);
            var abilities = attacker.ElementalAbilityHandler;
            if (upgradeElement != Element.None && abilities != null
                && abilities.IsUpgradeActive(upgradeElement))
                magnitude *= Mathf.Max(1f, upgradeBiteMultiplier);
            if (magnitude >= 0f) return;

            // The magnitude is authored negative because it reads as a debuff; a transfer takes a
            // positive amount. Classed VesselContact, the ward the reporter's gate asked about.
            ElementalTransfer.ApplyAuthoritative(ElementalTransferForm.Steal, victim, attacker,
                                                 ElementalTransfer.MaskOf(elements), -magnitude,
                                                 Vector3.zero, ElementalDebuffSources.VesselContact);
        }

        /// <summary>The element-scaled bite multiplier, at the pilot's replicated level.</summary>
        float BiteScale(IVesselStatus pilot) => Mathf.Max(0f, biteScale.EvaluateReplicated(pilot));
    }
}
