using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public class ShipHUD : MonoBehaviour // TODO: remove this class (unneeded) 
    {
        [SerializeField] Ship ship;
        void Start()
        {

            var shipHUD = GetComponentInChildren<Game.UI.MiniGameHUD>();

            if (ship.ShipStatus.Player.GameCanvas != null)
            {
                // Disable the default HUD
                ship.ShipStatus.Player.GameCanvas.MiniGameHUD.gameObject.SetActive(false);

                // Enable the modified HUD in the child
                shipHUD.gameObject.SetActive(true);

                // Assign the modified HUD to the ship's player
                ship.ShipStatus.Player.GameCanvas.MiniGameHUD = shipHUD;
            }
        }
    }
}
