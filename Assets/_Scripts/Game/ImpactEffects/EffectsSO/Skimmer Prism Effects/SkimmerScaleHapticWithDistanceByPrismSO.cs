using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerScaleHapticWithDistanceByPrism", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleHapticWithDistanceByPrismSO")]
    public class SkimmerScaleHapticWithDistanceByPrismSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            if (!skimmerVesselStatus.IsLocalUser || skimmerVesselStatus.AutoPilotEnabled) return;
            var hapticScale = impactor.CombinedWeight / 3;
            HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime);
        }
    }
}