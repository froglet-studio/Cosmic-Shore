using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerModifyThrotleByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerModifyThrotleByPrismEffectSO")]
    public class SkimmerModifyThrotleByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            impactor.Skimmer.VesselStatus.VesselTransformer.ModifyThrottle(prismImpactee.Prism.TrailBlockProperties.speedDebuffAmount, _speedModifierDuration);
        }
    }
}