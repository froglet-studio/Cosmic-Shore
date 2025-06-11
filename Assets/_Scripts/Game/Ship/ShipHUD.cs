using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore
{
    public class ShipHUD : MonoBehaviour // TODO: remove this class (unneeded) 
    {
        [SerializeField] Ship ship;

        [SerializeField]
        ShipHUDEventChannelSO onShipHUDInitialized;

        void Start()
        {

            var shipHUD = GetComponentInChildren<Game.UI.MiniGameHUD>();

            if (shipHUD == null)
            {
                return;
            }
            // TODO - Remove GameCanvas dependency

            /*if (ship.ShipStatus.Player.GameCanvas != null)
            {
                // Disable the default HUD
                ship.ShipStatus.Player.GameCanvas.MiniGameHUD.gameObject.SetActive(false);

                // Enable the modified HUD in the child
                shipHUD.gameObject.SetActive(true);

                // Assign the modified HUD to the ship's player
                ship.ShipStatus.Player.GameCanvas.MiniGameHUD = shipHUD;
            }*/

            shipHUD.gameObject.SetActive(true);
            onShipHUDInitialized.RaiseEvent(new ShipHUDData()
            {
                ShipHUD = shipHUD
            });
        }
    }
}
