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

        [Tooltip("Multiplies the capped slow strength when the impacted prism is dangerous, so the danger prism max is this many times higher (clamps at a full stop)")]
        [SerializeField] float dangerSlowMultiplier = 3f;

        [Tooltip("Multiplies the recovery duration when the impacted prism is dangerous (1 = same window as normal prisms)")]
        [SerializeField] float dangerSlowDurationMultiplier = 1f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;

            // Larger prisms slow more, up to maxSlowStrength. Danger prisms multiply the
            // already-capped strength, so their effective max is dangerSlowMultiplier times
            // higher, and suffer a longer recovery window.
            var slowStrength = Mathf.Min(trailBlockProperties.volume * massScaling, maxSlowStrength);
            var duration = speedModifierDuration;

            if (trailBlockProperties.IsDangerous)
            {
                slowStrength *= dangerSlowMultiplier;
                duration *= dangerSlowDurationMultiplier;
            }

            shipStatus.VesselTransformer.ModifyThrottle(Mathf.Clamp01(1f - slowStrength), duration);
        }
    }
}
