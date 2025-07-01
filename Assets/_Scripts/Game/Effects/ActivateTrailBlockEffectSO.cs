using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ActivateTrailBlockImpactEffect", menuName = "ScriptableObjects/Impact Effects/ActivateTrailBlockImpactEffectSO")]
    public class ActivateTrailBlockEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            throw new System.Exception("ActivateTrailBlockEffectSO should not be executed directly. Use ActivateTrailBlockAction instead.");
        }
    }
}
