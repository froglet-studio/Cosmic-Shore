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
    /// impactor, same magnitudes, same decay, same per-victim anti-spam.
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
        [Tooltip("Signed level change applied to every element (negative = debuff).")]
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
