using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AdjustLevelEffect", menuName = "ScriptableObjects/Impact Effects/AdjustLevelEffectSO")]
    public class AdjustLevelEffectSO : ImpactEffectSO
    {
        [SerializeField] int LevelAdjustment;

        /*public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ResourceSystem.AdjustLevel(crystalProperties.Element, LevelAdjustment);
        }*/

        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
