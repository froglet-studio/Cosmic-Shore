using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AdjustLevelEffect", menuName = "ScriptableObjects/Impact Effects/AdjustLevelEffectSO")]
    public class AdjustLevelEffectSO : BaseImpactEffectSO
    {
        public int LevelAdjustment;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.AdjustLevel(context.CrystalProperties.Element, LevelAdjustment);
        }
    }
}
