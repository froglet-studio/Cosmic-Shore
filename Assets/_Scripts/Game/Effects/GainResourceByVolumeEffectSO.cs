using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainResourceByVolumeImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainResourceByVolumeImpactEffectSO")]
    public class GainResourceByVolumeEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _boostResourceIndex;

        [SerializeField]
        float _blockChargeChange;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_boostResourceIndex, _blockChargeChange);
        }
    }
}
