using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Danger prism impact effect: when a vessel collides with a dangerous prism, all four of its
    /// elements receive a temporary, decaying debuff via the standardized
    /// <see cref="ResourceSystem.ApplyElementalEffect"/>. Danger prisms are friendly-fire - the
    /// debuff applies regardless of the prism's domain, own trail included. The debuff decays back
    /// to the vessel's base levels over <see cref="debuffDuration"/>; persistent progress is untouched.
    ///
    /// It is applied as <see cref="ElementalDebuffSources.DangerPrism"/>, so a ward held against
    /// the ARENA alone (the Dolphin's Drift Ward) stops it while leaving that vessel fully
    /// debuffable by another pilot's weapon.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselElementalDebuffByDangerPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselElementalDebuffByDangerPrismEffectSO")]
    public class VesselElementalDebuffByDangerPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Debuff Settings")]
        [Tooltip("Signed level change applied to every element (negative = debuff)")]
        [SerializeField] private float debuffMagnitude = -0.5f;

        [Tooltip("Seconds over which the temporary debuff decays back to baseline")]
        [SerializeField] private float debuffDuration = 4f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between danger prism debuffs on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        static readonly Element[] AllElements =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Per-vessel anti-spam: last time a danger prism debuff was applied to a vessel.
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            if (!vesselImpactor || !prismImpactee || vesselImpactor.Vessel == null)
                return;

            if (!prismImpactee.Prism.prismProperties.IsDangerous)
                return;

            var rs = vesselImpactor.Vessel.VesselStatus?.ResourceSystem;
            if (rs == null) return;

            // Cooldown check - anti-spam per debuffed vessel
            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // Classed DangerPrism, which is what a narrow ward can be held against: the Dolphin's
            // Time-5 Drift Ward wards THIS and nothing else (ElementalDebuffSources).
            for (int i = 0; i < AllElements.Length; i++)
                rs.ApplyElementalEffect(AllElements[i], debuffMagnitude, debuffDuration,
                                        ElementalDebuffSources.DangerPrism);
        }
    }
}
