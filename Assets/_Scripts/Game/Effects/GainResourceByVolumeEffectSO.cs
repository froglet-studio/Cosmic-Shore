using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainResourceByVolumeImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainResourceByVolumeImpactEffectSO")]
    public class GainResourceByVolumeEffectSO : ImpactEffectSO, IBaseImpactEffect
    {
        [SerializeField]
        int _boostResourceIndex;

        [SerializeField]
        float _blockChargeChange;

        public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_boostResourceIndex, _blockChargeChange);
        }
    }
}
