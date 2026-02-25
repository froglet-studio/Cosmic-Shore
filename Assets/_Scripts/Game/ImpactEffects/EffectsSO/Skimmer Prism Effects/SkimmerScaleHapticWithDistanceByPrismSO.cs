using CosmicShore.Game.IO;
using UnityEngine;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects
{
    [CreateAssetMenu(fileName = "SkimmerScaleHapticWithDistanceByPrism", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerScaleHapticWithDistanceByPrismSO")]
    public class SkimmerScaleHapticWithDistanceByPrismSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmerVesselStatus = impactor.Skimmer.VesselStatus;
            var hapticScale = impactor.CombinedWeight / 3;
            if (!skimmerVesselStatus.AutoPilotEnabled)
                HapticController.PlayConstant(hapticScale, hapticScale, Time.deltaTime);
        }
    }
}