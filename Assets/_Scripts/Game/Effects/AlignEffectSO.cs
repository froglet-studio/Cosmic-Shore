using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AlignImpactEffect", menuName = "ScriptableObjects/Impact Effects/AlignImpactEffectSO")]
    public class AlignEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
