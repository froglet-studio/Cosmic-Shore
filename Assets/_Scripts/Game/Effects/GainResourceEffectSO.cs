using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainResourceImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainResourceImpactEffectSO")]
    public class GainResourceEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        int _blockChargeChange;

        public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _blockChargeChange);
        }
    }
}
