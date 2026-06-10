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

        [Tooltip("Multiplies the slow's duration when the impacted prism is dangerous (1 = same as normal prisms)")]
        [SerializeField] float dangerSlowMultiplier = 3f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Vessel.VesselStatus;
            var trailBlockProperties = prismImpactee.Prism.prismProperties;

            var duration = trailBlockProperties.IsDangerous
                ? speedModifierDuration * dangerSlowMultiplier
                : speedModifierDuration;

            shipStatus.VesselTransformer.ModifyThrottle(Mathf.Min(trailBlockProperties.volume * massScaling, .2f), duration);
        }
    }
}
