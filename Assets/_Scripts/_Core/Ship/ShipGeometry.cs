using UnityEngine;

namespace StarWriter.Core
{
    public class ShipGeometry : MonoBehaviour
    {
        [SerializeField] public Ship Ship;

        void Start()
        {
            Ship.RegisterShipGeometry(this);
        }
    }
}