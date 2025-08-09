using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StealCrystalImpactEffect", menuName = "ScriptableObjects/Impact Effects/StealCrystalImpactEffectSO")]
    public class StealCrystalEffectSO : ImpactEffectSO
    {
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
