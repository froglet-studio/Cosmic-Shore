using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "GainResourceImpactEffect", menuName = "ScriptableObjects/Impact Effects/GainResourceImpactEffectSO")]
    public class GainResourceEffectSO : ImpactEffectSO
    {
        [SerializeField]
        int _resourceIndex;

        [SerializeField]
        int _blockChargeChange;

        /*public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_resourceIndex, _blockChargeChange);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
