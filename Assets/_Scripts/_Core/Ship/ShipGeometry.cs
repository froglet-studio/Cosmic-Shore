using UnityEngine;

public class ShipGeometry : MonoBehaviour
{
    [SerializeField] public Ship Ship;

    private void Start()
    {
        Ship.RegisterShipGeometry(this);
    }
}