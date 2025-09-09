using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipGentleSpinByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipGentleSpinByOtherEffectSO")]
    public abstract class ShipGentleSpinByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField, Range(0f, 180f)] float angleDegrees = 45f; // set in Inspector

        public void Execute(VesselImpactor impactor, ImpactorBase impactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var transform  = shipStatus.Transform;

            float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Vector3 dir = Quaternion.AngleAxis(sign * angleDegrees, transform.up) * transform.forward;

            shipStatus.ShipTransformer.SpinShip(dir);
        }
    }
}