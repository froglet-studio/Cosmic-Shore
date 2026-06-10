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
        [SerializeField] float speedModifierDuration = .03f;
        [SerializeField] float massScaling = .01f;

        [Tooltip("Maximum slow strength from a normal prism (fraction of speed removed, 0..1)")]
        [SerializeField] float maxSlowStrength = 0.8f;

        [Tooltip("Multiplies the capped slow strength when the impacted prism is dangerous, so the danger prism max is this many times higher")]
        [SerializeField] float dangerSlowMultiplier = 3f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;

            // Larger prisms slow more, up to maxSlowStrength. Danger prisms multiply the
            // already-capped strength, so their effective max is dangerSlowMultiplier times higher.
            var slowStrength = Mathf.Min(trailBlockProperties.volume * massScaling, maxSlowStrength);
            if (trailBlockProperties.IsDangerous)
                slowStrength *= dangerSlowMultiplier;

            shipStatus.VesselTransformer.ModifyThrottle(Mathf.Clamp01(1f - slowStrength), speedModifierDuration);
        }
    }
}
