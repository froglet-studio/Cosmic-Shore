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

            _change.ApplyTo(rs, this, ResolveDangerBonus(status, prismImpactee));
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
