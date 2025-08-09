using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "IncrementLevelImpactEffect", menuName = "ScriptableObjects/Impact Effects/IncrementLevelImpactEffectSO")]
    public class IncrementLevelEffectSO : ImpactEffectSO
    {
        /*public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
