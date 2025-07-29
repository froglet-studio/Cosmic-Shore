using CosmicShore.Core;
using TMPro;
using UnityEngine;


namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class ShipSelection : MonoBehaviour
    {
        TMP_Dropdown dropdown;

        // Start is called before the first frame update
        void Start()
        {
            dropdown = GetComponent<TMP_Dropdown>();

            // TODO - Get from separate data container. Don't access Hanger directly.
            dropdown.value = (int)Hangar.Instance.ChoosenClassType;
        }

        // TODO - Store in separate data container. Don't access Hanger directly.
        public void HangarSetPlayerShip(int shipClassType)
        {
            Hangar.Instance.SetPlayerShip(shipClassType);
        }
    }
}