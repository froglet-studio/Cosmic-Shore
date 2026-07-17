using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Proximity-scaled variant of the skim pulse: each prism entering the
    /// skimmer fires one pulse whose strength grows the closer the prism passes
    /// to the skimmer's center — hugging the trail feels stronger than grazing
    /// its edge, and a dense trail chains the pulses into a continuous
    /// rewarding train. (The simpler SkimmerHapticsByPrismEffect fires the same
    /// pulse at fixed strength; wire one or the other, not both.)
    ///
    /// Note: this used to read SkimmerImpactor.CombinedWeight, which is never
    /// written since the block-stay rework — the haptic silently played at zero.
    /// Proximity is now computed directly from the prism's position.
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerScaleHapticWithDistanceByPrism", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleHapticWithDistanceByPrismSO")]
    public class SkimmerScaleHapticWithDistanceByPrismSO : SkimmerPrismEffectSO
    {
        [SerializeField, Range(0f, 1f), Tooltip(
            "Pulse strength for a prism entering at the very edge of the " +
            "skimmer sphere. Center passes always pulse at full strength.")]
        float edgeStrength = 0.35f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            // Local pilot only — a remote player's skim must not buzz this device.
            if (!HapticController.IsLocalHumanPilot(skimmerVesselStatus)) return;

            var orchestrator = AudioHapticsOrchestrator.Instance;
            if (orchestrator == null) return;

            // Closeness of this prism to the skimmer center, 0 at the sphere's
            // edge, 1 dead-center. The skimmer trigger is a sphere collider of
            // radius 0.5 scaled by the transform.
            var skimmerTransform = impactor.Skimmer.transform;
            float radius = Mathf.Max(0.01f, skimmerTransform.localScale.x * 0.5f);
            float distance = Vector3.Distance(skimmerTransform.position, prismImpactee.transform.position);
            float closeness = 1f - Mathf.Clamp01(distance / radius);

            orchestrator.PlaySkimPulse(Mathf.Lerp(edgeStrength, 1f, closeness));
        }
    }
}
