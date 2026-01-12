using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselResetBoostPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselResetBoostPrismEffectSO")]
    public class VesselResetBoostPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Shared Config (single source of truth)")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier;
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;

            var baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult  = boostMaxMultiplier  != null ? boostMaxMultiplier.Value  : 5f;

            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            status.IsBoosting = false;
            status.BoostMultiplier = baseMult;

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = status.BoostMultiplier,
                MaxMultiplier = maxMult
            });
        }
    }
}