using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ReduceSpeedImpactEffect", menuName = "ScriptableObjects/Impact Effects/ReduceSpeedImpactEffectSO")]
    public class ReduceSpeedEffectSO : ImpactEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        /*public void Execute(ImpactEffectData data)
        {
            data.ThisShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);  // TODO: Magic numbers
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
