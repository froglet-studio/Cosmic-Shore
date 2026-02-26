using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
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