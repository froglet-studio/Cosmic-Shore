using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerVisualizeDistancePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerVisualizeDistancePrismEffect")]
    public class SkimmerVisualizeDistancePrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private int resourceIndex = 0;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var rs = impactor.Skimmer.VesselStatus.ResourceSystem;
            if (rs == null || resourceIndex >= rs.Resources.Count) return;

            var skimmer = impactor.Skimmer;
            var sqrRadius = skimmer.transform.localScale.x * skimmer.transform.localScale.x / 4f;
            var sqrDist   = (skimmer.transform.position - prismImpactee.Prism.transform.position).sqrMagnitude;

            float combinedWeight = Mathf.InverseLerp(0f, sqrRadius, sqrDist);

            rs.ChangeResourceAmount(resourceIndex, -rs.Resources[resourceIndex].CurrentAmount);
            rs.ChangeResourceAmount(resourceIndex, combinedWeight);
        }
    }
}
