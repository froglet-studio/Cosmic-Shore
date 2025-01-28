using CosmicShore.Core;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore
{
    public class NudgeShard : MonoBehaviour
    {
        [SerializeField] float Displacement = 50f;
        [SerializeField] float Duration = 1f;

        private void Start()
        {
            var scale = transform.parent.localScale;
            Displacement = Displacement * scale.x * scale.y * scale.z;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.IsLayer("Ships"))
            {
                var ship = other.GetComponent<ShipGeometry>()?.Ship;
                if (ship != null)
                {
                    if (ship.GetShipType == ShipTypes.Squirrel)
                    {
                        var sign = Mathf.Sign(Vector3.Dot(transform.parent.forward, ship.Transform.forward));
                        ship.ShipTransformer.ModifyVelocity(sign * transform.parent.forward * Displacement, Duration);
                    }
                    else
                    {
                        ship.ShipTransformer.ModifyVelocity(transform.parent.forward * Displacement, Duration);
                    }
                }
            }
        }
    }
}
