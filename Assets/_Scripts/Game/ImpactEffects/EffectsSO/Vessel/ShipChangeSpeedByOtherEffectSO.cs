using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeSpeedByOtherEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeSpeedByOtherEffectSO")]
    public class ShipChangeSpeedByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        protected override void ExecuteTyped(ShipImpactor impactor, ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);
        }
    }
}