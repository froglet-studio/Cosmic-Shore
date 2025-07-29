using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AttachImpactEffect", menuName = "ScriptableObjects/Impact Effects/AttachImpactEffectSO")]
    public class AttachEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            if (data == null || trailBlockProperties == null)
            {
                Debug.LogError("AttachEffectSO.Execute called with null data or trailBlockProperties.");
                return;
            }

            var trailBlock = trailBlockProperties.trailBlock;

            if (trailBlock.Trail == null)
                return;

            data.ThisShipStatus.Attached = true;
            data.ThisShipStatus.AttachedTrailBlock = trailBlock;
        }
    }
}
