using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerScaleGapByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleGapByPrismEffectSO")]
    public class SkimmerScaleGapByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float initialGap;
        [SerializeField] private float combinedWeight;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            skimmerVesselStatus.PrismSpawner.Gap = Mathf.Lerp(initialGap, skimmerVesselStatus.PrismSpawner.MinimumGap, impactor.CombinedWeight);
        }
    }
}