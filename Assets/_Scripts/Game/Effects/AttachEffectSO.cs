using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AttachImpactEffect", menuName = "ScriptableObjects/Impact Effects/AttachImpactEffectSO")]
    public class AttachEffectSO : BaseImpactEffectSO
    {
        public override void Execute(ImpactContext context)
        {
            if (context == null || context.ShipStatus == null || context.TrailBlockProperties == null)
                return;

            var trailBlock = context.TrailBlockProperties.trailBlock;

            if (trailBlock.Trail == null)
                return;

            context.ShipStatus.Attached = true;
            context.ShipStatus.AttachedTrailBlock = trailBlock;
        }
    }
}
