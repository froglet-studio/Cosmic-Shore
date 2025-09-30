using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerScalePitchAndYawPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScalePitchAndYawPrismEffectSO")]
    public class SkimmerScalePitchAndYawPrismEffectSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            skimmerVesselStatus.VesselTransformer.PitchScaler = skimmerVesselStatus.VesselTransformer.YawScaler = 150 + (120 * impactor.CombinedWeight);
        }
    }
}