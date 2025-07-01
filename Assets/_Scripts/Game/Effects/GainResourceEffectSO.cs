using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainResourceImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainResourceImpactEffectSO")]
    public class GainResourceEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        int _blockChargeChange;

        public override void Execute(ImpactContext context)
        {
            context.ShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _blockChargeChange);
        }
    }
}
