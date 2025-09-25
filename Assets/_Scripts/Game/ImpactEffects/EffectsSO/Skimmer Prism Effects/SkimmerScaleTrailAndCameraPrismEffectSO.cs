using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerScaleTrailAndCameraPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleTrailAndCameraPrismEffectSO")]
    public class SkimmerScaleTrailAndCameraPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        [SerializeField] private float sqrRadius;
        [SerializeField] private float minMatureBlockSqrDistance;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = impactor.Skimmer.VesselStatus;
            var normalizedDistance = Mathf.InverseLerp(15f, sqrRadius, minMatureBlockSqrDistance);
            vesselStatus.TrailSpawner.SetNormalizedXScale(normalizedDistance);

            // if (cameraManager != null && !vesselStatus.AutoPilotEnabled) 
            //     cameraManager.SetNormalizedCloseCameraDistance(normalizedDistance);
        }
    }
}