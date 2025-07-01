using CosmicShore.Core;
using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FeelDangerImpactEffect", menuName = "ScriptableObjects/Impact Effects/FeelDangerImpactEffectSO")]
    public class FeelDangerEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            if (context.TrailBlockProperties.IsDangerous && context.TrailBlockProperties.trailBlock.Team != context.ShipStatus.Team)
            {
                context.ShipStatus.ShipTransformer.ModifyThrottle(context.TrailBlockProperties.speedDebuffAmount, 1.5f);
            }
        }
    }
}
