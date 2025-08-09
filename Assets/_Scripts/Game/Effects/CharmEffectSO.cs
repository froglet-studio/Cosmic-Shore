using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "CharmImpactEffect", menuName = "ScriptableObjects/Impact Effects/CharmImpactEffectSO")]
    public class CharmEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
