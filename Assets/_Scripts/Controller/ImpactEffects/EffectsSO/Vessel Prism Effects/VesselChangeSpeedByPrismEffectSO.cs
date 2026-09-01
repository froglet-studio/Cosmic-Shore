using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using System.Linq;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeSpeedByPrismEffectSO")]
    public class VesselChangeSpeedByPrismEffectSO : VesselPrismEffectSO
    {
        [Tooltip("Seconds the slow takes to ease back to full speed. The vessel runs at the reduced speed immediately on impact and recovers linearly over this window.")]
        [SerializeField] float speedModifierDuration = .03f;

        [Tooltip("Slow strength per unit of prism volume (strength = volume * massScaling, before the cap)")]
        [SerializeField] float massScaling = .01f;

        [Tooltip("Maximum slow strength from a normal prism (fraction of speed removed, 0..1)")]
        [SerializeField] float maxSlowStrength = 0.8f;

        [Tooltip("Danger prisms always slow at maxSlowStrength times this multiplier - volume-independent, so even small danger prisms hit at the danger max (clamps at a full stop)")]
        [SerializeField] float dangerSlowMultiplier = 3f;

        [Tooltip("Multiplies the recovery duration when the impacted prism is dangerous (1 = same window as normal prisms)")]
        [SerializeField] float dangerSlowDurationMultiplier = 1f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;

            // Don't brake the vessel on its OWN trail - you skim your own mass, you don't plow through it.
            // Without this, flying into your own large (volume-scaled → up to maxSlowStrength) prism -
            // e.g. one the Astro League ball shielded, which makes own trail collidable - hard-brakes you,
            // and oscillating at its edge re-triggers the slow into a stutter. DANGER prisms are exempt:
            // they slow everyone regardless of domain (locked design - danger is not safe to its own domain).
            if (!trailBlockProperties.IsDangerous && prismImpactee.Prism.Domain == shipStatus.Domain)
                return;

            // Normal prisms: larger volume slows more, up to maxSlowStrength.
            // Danger prisms: always the danger max (maxSlowStrength * dangerSlowMultiplier),
            // independent of volume - danger is a prism state, not a function of its mass -
            // with a longer recovery window.
            float slowStrength;
            float duration;

            if (trailBlockProperties.IsDangerous)
            {
                slowStrength = maxSlowStrength * dangerSlowMultiplier;
                duration = speedModifierDuration * dangerSlowDurationMultiplier;
            }
            else
            {
                slowStrength = Mathf.Min(trailBlockProperties.volume * massScaling, maxSlowStrength);
                duration = speedModifierDuration;
            }

            shipStatus.VesselTransformer.ModifyThrottle(Mathf.Clamp01(1f - slowStrength), duration);
        }
    }
}
