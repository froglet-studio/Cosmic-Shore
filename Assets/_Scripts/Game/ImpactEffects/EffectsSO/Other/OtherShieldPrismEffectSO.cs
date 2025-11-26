using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OtherShieldPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Other/OtherShieldPrismEffectSO")]
    public class OtherShieldPrismEffectSO : ImpactEffectSO<ImpactorBase, PrismImpactor>
    {
        protected override void ExecuteTyped(ImpactorBase impactor, PrismImpactor impactee)
        {
            var trailBlockProperties = impactee.Prism.TrailBlockProperties;
            if (trailBlockProperties == null || trailBlockProperties.trailBlock == null)
            {
                Debug.LogWarning("ShieldEffectSO: trailBlockProperties or trailBlock is null");
                return;
            }

            trailBlockProperties.trailBlock.ActivateShield(.5f);
        }
    }
}