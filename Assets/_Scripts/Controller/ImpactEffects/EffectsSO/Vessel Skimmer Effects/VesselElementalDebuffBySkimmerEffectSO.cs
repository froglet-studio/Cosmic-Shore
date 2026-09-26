using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Scale Dust</b>, pilot half — CHARGE. An opposing pilot caught inside the
    /// wings takes a temporary, decaying debuff on every element through the standardized
    /// <see cref="ResourceSystem.ApplyElementalEffect"/>. The skimmer sibling of
    /// <see cref="VesselElementalDebuffByExplosionEffectSO"/>, same decay, same per-victim
    /// anti-spam.
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
    public class VesselElementalDebuffBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Debuff Settings")]
        [Tooltip("Signed level change applied to every element the dust drains (negative = " +
                 "debuff). DERIVED, not authored: a hit's bite tracks the price Broadside puts " +
                 "on its verb — total drain = points x (2.0/12), spread over the elements " +
                 "touched. The Butterfly's dust is a Strike-class contact (8 points), so the " +
                 "total is 1.3333 and over four elements that is -0.333333 each. See " +
                 "Tools/Build/author_combat_debuff_magnitudes.py.")]
        [SerializeField] float debuffMagnitude = -0.3333333f;

        [Tooltip("Seconds over which the temporary debuff decays back to baseline.")]
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
        [Tooltip("Minimum seconds between dust debuffs on the same vessel. A skimmer sphere " +
                 "overlaps a hull for many frames and a hull is several colliders, so a non-zero " +
                 "window is effectively mandatory here.")]
        [SerializeField, Min(0f)] float cooldown = 1f;

        // Per-vessel anti-spam, keyed on the victim's ResourceSystem — the same shape the
        // explosion and danger-prism debuffs use, so the three share a mental model.
        static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

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
            if (rs == null || elements == null) return;

            float now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // Per-hit snapshot of the upgrade, read through the REPLICATED unlock bit rather
            // than a raw local level read: the drain lands on the VICTIM's machine as well as
            // the attacker's, and two peers disagreeing about how hard it bit is two different
            // element levels for the same pilot.
            float magnitude = debuffMagnitude * BiteScale(pilot);
            var abilities = pilot.ElementalAbilityHandler;
            if (upgradeElement != Element.None && abilities != null
                && abilities.IsUpgradeActive(upgradeElement))
                magnitude *= Mathf.Max(1f, upgradeBiteMultiplier);

            for (int i = 0; i < elements.Length; i++)
                rs.ApplyElementalEffect(elements[i], magnitude, debuffDuration,
                                        ElementalDebuffSources.VesselContact);
        }

        /// <summary>The element-scaled bite multiplier, at the pilot's replicated level.</summary>
        float BiteScale(IVesselStatus pilot) => Mathf.Max(0f, biteScale.EvaluateReplicated(pilot));
    }
}
