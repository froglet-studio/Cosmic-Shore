using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerModifyThrotleByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerModifyThrotleByPrismEffectSO")]
    public class SkimmerModifyThrotleByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;
        [SerializeField] 
        float _speedBuffAmount = 2.5f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = impactor.Skimmer.VesselStatus;
            vesselStatus.Boosting = true;
            vesselStatus.BoostMultiplier = 1 + (_speedBuffAmount * impactor.CombinedWeight);
        }
    }
}