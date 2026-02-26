using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects
{
    [CreateAssetMenu(fileName = "SkimmerHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerHapticsByPrismEffectSO")]
    public class SkimmerHapticsByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            _haptic.PlayIfManual(impactor.Skimmer.VesselStatus);
        }
    }
}