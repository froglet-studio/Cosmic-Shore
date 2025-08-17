using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PrismShieldByOtherEffect", menuName = "ScriptableObjects/Impact Effects/PrismShieldByOtherEffectSO")]
    public class PrismShieldByOtherEffectSO : ImpactEffectSO<PrismImpactor, ImpactorBase>
    {
        protected override void ExecuteTyped(PrismImpactor impactor, ImpactorBase crystalImpactee)
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
