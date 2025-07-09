using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AdjustLevelEffect", menuName = "ScriptableObjects/Impact Effects/AdjustLevelEffectSO")]
    public class AdjustLevelEffectSO : ImpactEffectSO, ICrystalImpactEffect
    {
        [SerializeField] int LevelAdjustment;

        public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.AdjustLevel(crystalProperties.Element, LevelAdjustment);
        }
    }
}
