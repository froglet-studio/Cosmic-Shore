using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerScaleTrailAndCameraPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleTrailAndCameraPrismEffect")]
    public class SkimmerScaleTrailPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float minSqrDistance = 15f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmer = impactor.Skimmer;
            var sqrRadius = skimmer.transform.localScale.x * skimmer.transform.localScale.x / 4f;
            var sqrDist   = (skimmer.transform.position - prismImpactee.Prism.transform.position).sqrMagnitude;

            float normalized = Mathf.InverseLerp(minSqrDistance, sqrRadius, sqrDist);

            skimmer.VesselStatus.PrismSpawner.SetNormalizedXScale(normalized);

            // var camManager = CameraManager.Instance;
            // if (camManager != null && !skimmer.Ship.ShipStatus.AutoPilotEnabled)
            //     camManager.SetNormalizedCloseCameraDistance(normalized);
        }
    }
}
