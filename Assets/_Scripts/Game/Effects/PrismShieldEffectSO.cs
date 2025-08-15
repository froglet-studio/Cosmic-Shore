using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PrismShieldEffect", menuName = "ScriptableObjects/Impact Effects/PrismShieldEffectSO")]
    public class PrismShieldEffectSO : ImpactEffectSO<R_PrismImpactor, R_ImpactorBase>
    {
        protected override void ExecuteTyped(R_PrismImpactor impactor, R_ImpactorBase crystalImpactee)
        {
            var trailBlockProperties = impactor.Prism.TrailBlockProperties;
            if (trailBlockProperties == null || trailBlockProperties.trailBlock == null)
            {
                Debug.LogWarning("ShieldEffectSO: trailBlockProperties or trailBlock is null");
                return;
            }

            trailBlockProperties.trailBlock.ActivateShield(.5f);
        }
    }
}
