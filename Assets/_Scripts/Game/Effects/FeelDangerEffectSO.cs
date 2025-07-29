using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FeelDangerImpactEffect", menuName = "ScriptableObjects/Impact Effects/FeelDangerImpactEffectSO")]
    public class FeelDangerEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (trailBlockProperties.IsDangerous && trailBlockProperties.trailBlock.Team != data.ThisShipStatus.Team)
            {
                data.ThisShipStatus.ShipTransformer.ModifyThrottle(trailBlockProperties.speedDebuffAmount, 1.5f);
            }
        }
    }
}
