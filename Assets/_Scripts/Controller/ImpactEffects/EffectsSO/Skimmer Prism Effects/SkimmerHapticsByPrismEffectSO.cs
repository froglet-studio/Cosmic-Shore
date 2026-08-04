using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The reward feel: every prism entering the skimmer fires one bright, proximity-scaled skim
    /// pulse. Wired into the Squirrel's skimmer-prism effect list, so a dense skim reads as a rapid,
    /// continuously rewarding pulse train (the gate in <see cref="HapticController.PlaySkim"/> rate-
    /// limits and de-conflicts it). Local human pilot only — remote/AI skims never buzz this device.
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerHapticsByPrismEffectSO")]
    public class SkimmerHapticsByPrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Pulse strength: edge-of-sphere floor → dead-centre ceiling (1.0)")]
        [Range(0f, 1f)] [SerializeField] float minStrength = 0.35f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            if (status == null || !status.IsLocalUser || status.AutoPilotEnabled) return;
            if (prismImpactee == null || prismImpactee.Prism == null) return;

            // Proximity from prism position vs the skimmer sphere: 1 at the centre, 0 at the surface.
            Vector3 center = impactor.transform.position;
            float radius = impactor.SphereWorldRadius;
            float closeness = radius > 0.0001f
                ? Mathf.Clamp01(1f - Vector3.Distance(prismImpactee.Prism.transform.position, center) / radius)
                : 1f;

            HapticController.PlaySkim(Mathf.Lerp(minStrength, 1f, closeness));
        }
    }
}
