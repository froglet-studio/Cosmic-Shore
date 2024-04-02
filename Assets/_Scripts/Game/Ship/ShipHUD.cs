using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class ShipHUD : MonoBehaviour
    {
        [SerializeField] Ship ship;
        void Start()
        {

            var shipHUD = GetComponentInChildren<Game.UI.MiniGameHUD>();

            if (ship.Player.GameCanvas != null)
            {
                // Disable the default HUD
                ship.Player.GameCanvas.MiniGameHUD.gameObject.SetActive(false);

                // Enable the modified HUD in the child
                shipHUD.gameObject.SetActive(true);

                // Assign the modified HUD to the ship's player
                ship.Player.GameCanvas.MiniGameHUD = shipHUD;
            }
        }
    }
}
