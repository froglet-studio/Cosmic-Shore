using StarWriter.Core.HangerBuilder;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class ShipSelection : MonoBehaviour
{
    TMP_Dropdown dropdown;

    // Start is called before the first frame update
    void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.value = (int) Hangar.Instance.GetPlayerShip();
    }

    public void HangarSetPlayerShip(int shipType)
    {
        Hangar.Instance.SetPlayerShip(shipType);
    }
}
