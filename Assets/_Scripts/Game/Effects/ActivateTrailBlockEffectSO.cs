using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ActivateTrailBlockImpactEffect", menuName = "ScriptableObjects/Impact Effects/ActivateTrailBlockImpactEffectSO")]
    public class ActivateTrailBlockEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
