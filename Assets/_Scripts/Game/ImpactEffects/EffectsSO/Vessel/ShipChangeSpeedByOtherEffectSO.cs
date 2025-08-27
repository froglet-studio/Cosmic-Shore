using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeSpeedByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeSpeedByOtherEffectSO")]
    public class ShipChangeSpeedByOtherEffectSO : ShipOtherEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public override void Execute(ShipImpactor impactor, ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);
        }
    }
}
