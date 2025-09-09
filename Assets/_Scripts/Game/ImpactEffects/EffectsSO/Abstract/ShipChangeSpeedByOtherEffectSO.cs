using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipChangeSpeedByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipChangeSpeedByOtherEffectSO")]
    public abstract class ShipChangeSpeedByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public void Execute(VesselImpactor impactor, ImpactorBase impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);
        }
    }
}
