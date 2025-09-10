using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselGentleSpinByProjectileEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselGentleSpinByProjectileEffectSO")]
    public class VesselGentleSpinByProjectileEffectSO : VesselProjectileEffectSO
    {
        [SerializeField, Range(0f, 180f)] float angleDegrees = 45f; // set in Inspector

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var transform  = shipStatus.Transform;

            float sign = Random.value < 0.5f ? -1f : 1f;
            Vector3 dir = Quaternion.AngleAxis(sign * angleDegrees, transform.up) * transform.forward;

            shipStatus.ShipTransformer.SpinShip(dir);
        }
    }
}