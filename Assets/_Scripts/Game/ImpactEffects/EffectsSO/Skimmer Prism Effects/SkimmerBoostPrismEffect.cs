using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerBoostPrismEffect", menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerBoostPrismEffectSO")]
    public class SkimmerBoostPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] float _speedModifierValue = 0.1f;
        [SerializeField] float _maxBoostMultiplier = 5f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var vesselStatus = impactor.Skimmer.VesselStatus;
            vesselStatus.IsBoosting = true;
            // Add boost and clamp to maximum
            vesselStatus.BoostMultiplier = Mathf.Min(
                vesselStatus.BoostMultiplier + _speedModifierValue,
                _maxBoostMultiplier
            );
        }
    }
}

