using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
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