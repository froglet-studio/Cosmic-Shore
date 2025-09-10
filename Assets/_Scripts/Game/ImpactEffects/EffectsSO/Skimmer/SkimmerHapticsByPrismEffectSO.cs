using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerHapticsByPrismEffectSO")]
    public class SkimmerHapticsByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            _haptic.PlayIfManual(impactor.Skimmer.ShipStatus);
        }
    }
}