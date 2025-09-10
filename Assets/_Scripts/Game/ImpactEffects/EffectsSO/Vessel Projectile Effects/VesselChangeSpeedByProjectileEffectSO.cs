using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselChangeSpeedByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselChangeSpeedByProjectileEffectSO")]
    public class VesselChangeSpeedByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField] float _amount = .1f;
        [SerializeField] int _duration = 3;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            impactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(_amount, _duration);
        }
    }
}
