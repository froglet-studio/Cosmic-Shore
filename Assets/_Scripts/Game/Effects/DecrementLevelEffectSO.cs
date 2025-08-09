using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DecrementLevelImpactEffect", menuName = "ScriptableObjects/Impact Effects/DecrementLevelImpactEffectSO")]
    public class DecrementLevelEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
