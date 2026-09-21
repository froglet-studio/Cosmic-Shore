using UnityEngine;
using Obvious.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.UI;
using System.Linq;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerBoostPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerBoostPrismEffectSO")]
    public class SkimmerBoostPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float addPerHit = 0.1f;

        [Tooltip("Multiplies the energy gained per hit when the skimmed prism is dangerous. " +
                 "LOCKED behind the vessel's CHARGE level-5 elemental upgrade (map-gated via " +
                 "IsUpgradeActive) - below the unlock, danger prisms grant only the base energy.")]
        [SerializeField] private float dangerEnergyMultiplier = 10f;

        /// <summary>CHARGE -> skim energy per hit: x1 at the resting level, x2 at level 10, floored at x0.25.
        /// Migrated verbatim from the retired ElementalAbilityMapSO generic
        /// multiplier (atFull 2, minMultiplier 0.25) — see
        /// Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md. Lives here, on the
        /// asset that owns the parameter, so it can only ever scale this one number.
        /// Never bound (this is a ScriptableObject, and BindElementalFloats reflects only
        /// over ElementalShipComponent MonoBehaviours), so it holds no per-vessel state.
        /// </summary>
        [SerializeField] ElementalFloat chargeEnergyMultiplier =
            ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.25f);

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

            // CHARGE -> skim energy: the energy gained per prism-skimmer collision scales with
            // the vessel's live Charge level via its ElementalAbilityMapSO (1x at resting level,
            // 1x for vessels without a map or Charge entry). Per-hit snapshot; stateless SO.
            float add = addPerHit * chargeEnergyMultiplier.EvaluateLive(status);

            // CHARGE level-5 'Live Wire': danger prisms grant the bonus energy multiplier only
            // once the skimming vessel's Charge upgrade is active (below it, danger prisms pay
            // base energy - the risk stays, the 10x reward is earned).
            if (prismImpactee.Prism.prismProperties.IsDangerous
                && status?.ElementalAbilityHandler?.IsUpgradeActive(Element.Charge) == true)
                add *= dangerEnergyMultiplier;

            float next = status.BoostMultiplier + add;
            next = Mathf.Clamp(next, baseMult, maxMult);

            status.BoostMultiplier = next;

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = status.BoostMultiplier,
                MaxMultiplier = maxMult,
                SourceDomain = prismImpactee.OwnDomain,
                VesselStatus = status
            });
        }
    }
}