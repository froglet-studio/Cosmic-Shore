using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipReduceSpeedEffect", menuName = "ScriptableObjects/Impact Effects/ShipReduceSpeedEffectSO")]
    public class ShipReduceSpeedEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        protected override void ExecuteTyped(R_ShipImpactor impactor, R_ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);  // TODO: Magic numbers
        }
    }
}
