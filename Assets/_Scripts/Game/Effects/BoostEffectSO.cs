using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BoostImpactEffect", menuName = "ScriptableObjects/Impact Effects/BoostImpactEffectSO")]
    public class BoostEffectSO : ImpactEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        /*public void Execute(ImpactEffectData data, CrystalProperties crystalProperties)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(crystalProperties.speedBuffAmount, _speedModifierDuration);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
