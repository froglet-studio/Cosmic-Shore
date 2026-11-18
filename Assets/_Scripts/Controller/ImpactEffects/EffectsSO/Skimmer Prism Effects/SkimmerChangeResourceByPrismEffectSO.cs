using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Grants a resource for skimming a prism — for the Dolphin, the skim ENERGY it later spends in
    /// one shot on a crystal.
    ///
    /// Optionally pays a BONUS on danger prisms, gated behind an element's level-5 upgrade. Same
    /// shape as the Squirrel's "Live Wire" (<see cref="SkimmerBoostPrismEffectSO"/>): the risk of
    /// grazing a danger trail is always there, the extra reward is earned. Both fields default to
    /// off, so assets that predate them behave exactly as before.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkimmerChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerChangeResourceByPrismEffectSO")]
    public class SkimmerChangeResourceByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] ResourceChangeSpec _change;

        [Tooltip("Pay nothing for a SUPER-SHIELDED prism. Off (the default) pays for every skim, as before. On for the " +
                 "Swordfish's slice fuel (2026-11-18): a blade bound in super-shielded mass has sliced nothing.")]
        [SerializeField] bool _skipSuperShielded;

        [Tooltip("Multiplies every gain by an element's live level. Off (the default: Value 1) pays the authored amount " +
                 "at every level. The Swordfish's slice fuel scales with TIME (x1 at rest, x4.5 at level 10; 2026-11-18): " +
                 "Time is the sword's rate (Quickdraw), and a faster sword refuels its ramp faster.")]
        [SerializeField] ElementalFloat _gainMultiplier = new ElementalFloat(1f);

        [Header("Elemental — danger-prism bonus")]
        [Tooltip("The element whose LEVEL-5 upgrade unlocks the danger-prism bonus below. " +
                 "None (the default) disables the bonus entirely — danger prisms then pay the same " +
                 "as any other prism.")]
        [SerializeField] Element _dangerBonusElement = Element.None;

        [Tooltip("Multiplies the resource gained when the skimmed prism is DANGEROUS, once the " +
                 "element above has its level-5 upgrade. Below the unlock, danger prisms pay the " +
                 "base amount — the reward is earned, the risk is not.")]
        [SerializeField, Min(1f)] float _dangerBonusMultiplier = 1f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            var rs = status?.ResourceSystem;
            if (rs == null) return;
            if (_skipSuperShielded && !VesselGainResourceByPrismEffectSO.PaysFor(prismImpactee, true)) return;

            _change.ApplyTo(rs, this, ResolveDangerBonus(status, prismImpactee) * _gainMultiplier.EvaluateLive(status));
        }

        /// <summary>
        /// The per-hit gain multiplier: the danger bonus when this prism is dangerous AND the
        /// gating element's upgrade is live, otherwise 1.
        /// </summary>
        float ResolveDangerBonus(IVesselStatus status, PrismImpactor prismImpactee)
        {
            if (_dangerBonusElement == Element.None || _dangerBonusMultiplier <= 1f) return 1f;
            if (prismImpactee == null || prismImpactee.Prism == null) return 1f;
            if (!prismImpactee.Prism.prismProperties.IsDangerous) return 1f;

            var handler = status?.ElementalAbilityHandler;
            return handler && handler.IsUpgradeActive(_dangerBonusElement) ? _dangerBonusMultiplier : 1f;
        }
    }
}
