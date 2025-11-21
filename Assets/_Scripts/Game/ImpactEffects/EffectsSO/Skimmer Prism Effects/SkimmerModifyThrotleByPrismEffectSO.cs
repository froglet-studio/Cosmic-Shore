using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerModifyThrotleByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerModifyThrotleByPrismEffectSO")]
    public class SkimmerModifyThrotleByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField]
        float _speedModifierValue;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = impactor.Skimmer.VesselStatus;
            vesselStatus.IsBoosting = true;
            vesselStatus.BoostMultiplier = 1 + (_speedModifierValue * impactor.CombinedWeight);
        }
    }
}