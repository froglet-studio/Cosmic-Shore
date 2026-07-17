using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerScaleHapticWithDistanceByPrism", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleHapticWithDistanceByPrismSO")]
    public class SkimmerScaleHapticWithDistanceByPrismSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            // Local pilot only — a remote player's skim must not buzz this device.
            if (!HapticController.IsLocalHumanPilot(skimmerVesselStatus)) return;

            var hapticScale = impactor.CombinedWeight / 3;
            // Routed into the orchestrator's continuous bed: the per-frame call
            // modulates the bed level instead of reloading a clip every frame.
            // Held slightly longer than one frame so the drive bridges frames.
            HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime * 2f);
        }
    }
}