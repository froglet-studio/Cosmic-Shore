using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Explosion impact effect: a vessel caught in the blast takes a temporary, decaying debuff on
    /// every element through the standardized <see cref="ResourceSystem.ApplyElementalEffect"/>.
    ///
    /// This is the ELEMENTAL expression of "the blast debuffs you" — per the design philosophy,
    /// elementals are the single system that governs all buffing and debuffing, so a blast that
    /// wants to weaken a pilot reaches for that fundamental rather than inventing a per-blast
    /// status. It is the danger-prism debuff
    /// (<see cref="VesselElementalDebuffByDangerPrismEffectSO"/>) lifted onto the explosion
    /// impactor, same decay, same per-victim anti-spam.
    ///
    /// A HIT'S BITE TRACKS ITS PRICE. Every drain asset in the fleet used to carry a flat -0.5
    /// whatever the attack, so a Manta bloom worth 12 points bit HALF as deep as a Dolphin cone
    /// worth the same (it drains two elements, not four) and a warhead grazing for 10 drained
    /// exactly as hard as the cone. Magnitudes are now DERIVED from Broadside's price list by
    /// Tools/Build/author_combat_debuff_magnitudes.py — the TOTAL over the elements an attack
    /// touches is proportional to its points, so how an attack spreads its drain is free and
    /// what it is worth is not. Because that price list was itself tuned to flatten POINTS PER
    /// SECOND across the fleet, the drain inherits that balance instead of needing its own pass.
    /// Do not hand-edit a magnitude: the script's --check fails on drift.
    ///
    /// Domain filtering is NOT this effect's job: <see cref="ExplosionImpactor.AcceptImpactee"/>
    /// already declines own-domain vessels unless the blast is authored/overridden affectSelf, so
    /// adding a second domain test here would silently double-gate friendly fire.
    ///
    /// Elemental immunity is honoured inside ApplyElementalEffect, so a pilot warded against
    /// <see cref="ElementalDebuffSources.Explosion"/> eats the blast's other consequences and keeps
    /// their levels — the same contract danger prisms run under. The class matters: the Dolphin's
    /// Drift Ward covers <see cref="ElementalDebuffSources.DangerPrism"/> only, so drifting does
    /// NOT shrug off this blast (which is the entire scoring event of The Bends).
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalDebuffByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselElementalDebuffByExplosionEffectSO")]
    public class VesselElementalDebuffByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Debuff Settings")]
        [Tooltip("Signed level change applied to every element the blast drains (negative = " +
                 "debuff). DERIVED, not authored: a hit's bite tracks the price Broadside puts " +
                 "on its verb, so this is total drain / element count where total = points x " +
                 "(2.0 / 12). Edit it with Tools/Build/author_combat_debuff_magnitudes.py, " +
                 "whose --check FAILS on a hand-edit. The initializer is the ANCHOR itself " +
                 "(the 12-point Debuff class over four elements), so a new asset starts on the " +
                 "one play-tested value.")]
        [SerializeField] private float debuffMagnitude = -0.5f;

        [Tooltip("Seconds over which the temporary debuff decays back to baseline.")]
        [SerializeField] private float debuffDuration = 4f;

        [Tooltip("Which elements the blast drains. The initializer is ALL FOUR — every asset " +
                 "authored before this field existed (the Dolphin/Scarab blasts) deserializes " +
                 "to exactly its old behaviour. The Manta's bomb debuff authors Mass and " +
                 "Space only: the 04/20/2026 rule bars overtakers from touching Time, and " +
                 "Charge stays the victim's own weapon economy.")]
        [SerializeField] private Element[] elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between explosion debuffs on the same vessel. Blasts grow " +
                 "through a victim over several frames, so without this one detonation would " +
                 "stack its debuff every trigger re-entry.")]
        [SerializeField] private float cooldown = 1f;

        // Per-vessel anti-spam, keyed on the victim's ResourceSystem — the same shape the danger
        // prism debuff uses, so the two share a mental model even though the tables are separate.
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            if (!impactor || impactor.Vessel == null) return;

            var rs = impactor.Vessel.VesselStatus?.ResourceSystem;
            if (rs == null) return;

            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // Classed Explosion, NOT DangerPrism: a blast is a weapon another pilot aimed, and a
            // ward earned against the arena must not cancel one (ElementalDebuffSources).
            if (elements == null) return;
            for (int i = 0; i < elements.Length; i++)
                rs.ApplyElementalEffect(elements[i], debuffMagnitude, debuffDuration,
                                        ElementalDebuffSources.Explosion);
        }
    }
}
