using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "IncrementLevelImpactEffect", menuName = "ScriptableObjects/Impact Effects/IncrementLevelImpactEffectSO")]
    public class IncrementLevelEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.IncrementLevel(context.CrystalProperties.Element);
        }
    }
}
