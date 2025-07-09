using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShieldImpactEffect", menuName = "ScriptableObjects/Impact Effects/ShieldImpactEffectSO")]
    public class ShieldEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (trailBlockProperties == null || trailBlockProperties.trailBlock == null)
            {
                Debug.LogWarning("ShieldEffectSO: trailBlockProperties or trailBlock is null");
                return;
            }

            trailBlockProperties.trailBlock.ActivateShield(.5f);
        }
    }
}
