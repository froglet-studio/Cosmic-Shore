using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ChangeResourceImpactEffect", menuName = "ScriptableObjects/Impact Effects/ChangeResourceImpactEffectSO")]
    public class ChangeResourceEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        float _resourceAmount;

        public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}
