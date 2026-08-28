using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Overtake impact effect for the Squirrel's skimmer: when a slower vessel collides with the
    /// Squirrel's skimmer (the Squirrel is overtaking it), that vessel's elements get a temporary,
    /// decaying buff or debuff via the standardized <see cref="ResourceSystem.ApplyElementalEffect"/>.
    /// - Opponent (different domain): a temporary debuff (negative magnitude).
    /// - Ally (same domain): a temporary buff (positive magnitude).
    /// Both decay back to the vessel's base levels over <see cref="effectDuration"/>; persistent
    /// progress is untouched. Nothing happens to the faster (Squirrel) vessel.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselOvertakeBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselOvertakeBySkimmerEffectSO")]
    public class VesselOvertakeBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Effect")]
        [Tooltip("Signed level change applied to an overtaken opponent (negative = debuff)")]
        [SerializeField] private float debuffMagnitude = -0.5f;

        [Tooltip("Signed level change applied to an overtaken ally (positive = buff)")]
        [SerializeField] private float buffMagnitude = 0.5f;

        [Tooltip("Seconds over which the temporary buff/debuff decays back to baseline")]
        [SerializeField] private float effectDuration = 3f;

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.8f;
        [SerializeField] private float hapticFrequency = 0.7f;
        [SerializeField] private float hapticDuration = 0.25f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between overtake effects on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        // Per-vessel anti-spam: last time an overtake effect was applied to a vessel.
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();

        // No prune path — destroyed ResourceSystem keys accumulate for the editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _lastEffectTime.Clear();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null) return;
            if (impactee == null || impactee.Skimmer?.VesselStatus?.Vessel == null) return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Don't trigger on self-collision
            if (impactorVessel == impacteeVessel) return;

            // Only the slower vessel - the one being overtaken - is affected
            if (impactorVessel.VesselStatus.Speed >= impacteeVessel.VesselStatus.Speed) return;

            var overtakenStatus = impactorVessel.VesselStatus;
            var rs = overtakenStatus.ResourceSystem;
            if (rs == null) return;

            // Cooldown check - anti-spam per overtaken vessel
            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime) && now - lastTime < cooldown)
                return;
            _lastEffectTime[rs] = now;

            // Haptic feedback
            HapticController.PlayConstant(hapticAmplitude, hapticFrequency, hapticDuration);

            // Allies are buffed, opponents debuffed - both as temporary, decaying effects.
            bool isAlly = overtakenStatus.Domain == impacteeVessel.VesselStatus.Domain;
            float magnitude = isAlly ? buffMagnitude : debuffMagnitude;

            // Classed VesselContact - the source class only matters on the debuff branch, where it
            // decides which wards stop it (ElementalDebuffSources).
            for (int i = 0; i < AllElements.Length; i++)
                rs.ApplyElementalEffect(AllElements[i], magnitude, effectDuration,
                                        ElementalDebuffSources.VesselContact);

            // Friendly buff audio: all four elements are buffed at once, so play a
            // single representative element's buff SFX (chosen at random for variety)
            // rather than stacking a four-sound chord. Only the local ally being
            // buffed hears it.
            if (isAlly && overtakenStatus.IsLocalUser)
            {
                var element = AllElements[Random.Range(0, AllElements.Length)];
                AudioSystem.Instance?.PlayGameplaySFX(JoustBuffCategoryForElement(element));
            }
        }

        static GameplaySFXCategory JoustBuffCategoryForElement(Element element) => element switch
        {
            Element.Charge => GameplaySFXCategory.JoustBuffCharge,
            Element.Mass   => GameplaySFXCategory.JoustBuffMass,
            Element.Space  => GameplaySFXCategory.JoustBuffSpace,
            Element.Time   => GameplaySFXCategory.JoustBuffTime,
            _              => GameplaySFXCategory.JoustBuffCharge,
        };
    }
}
