using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerBoostPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerBoostPrismEffectSO")]
    public class SkimmerBoostPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float addPerHit = 0.1f;

        [Header("Shared Config (single source of truth)")]
        [SerializeField] private ScriptableVariable<float> boostBaseMultiplier; // initial/base
        [SerializeField] private ScriptableVariable<float> boostMaxMultiplier;  // max

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;

            float baseMult = boostBaseMultiplier != null ? boostBaseMultiplier.Value : 1f;
            float maxMult  = boostMaxMultiplier  != null ? boostMaxMultiplier.Value  : 5f;

            // HARD GUARDS (prevents “stuck forever”)
            baseMult = Mathf.Max(0.0001f, baseMult);
            maxMult  = Mathf.Max(baseMult, maxMult);

            status.IsBoosting = true;

            float next = status.BoostMultiplier + addPerHit;
            next = Mathf.Clamp(next, baseMult, maxMult);

            status.BoostMultiplier = next;

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = status.BoostMultiplier,
                MaxMultiplier = maxMult
            });
        }
    }
}