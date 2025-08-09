using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FireImpactEffect", menuName = "ScriptableObjects/Impact Effects/FireImpactEffectSO")]
    public class FireEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
