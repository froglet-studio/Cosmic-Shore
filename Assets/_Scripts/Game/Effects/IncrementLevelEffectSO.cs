using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "IncrementLevelImpactEffect", menuName = "ScriptableObjects/Impact Effects/IncrementLevelImpactEffectSO")]
    public class IncrementLevelEffectSO : ImpactEffectSO, ICrystalImpactEffect
    {
        public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.IncrementLevel(crystalProperties.Element);
        }
    }
}
