using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class ShipHUD : MonoBehaviour // TODO: remove this class (unneeded) 
    {
        [SerializeField] VesselController ship;

        // [SerializeField] ShipHUDEventChannelSO onShipHUDInitialized;
        [SerializeField] ScriptableEventShipHUDData onShipHUDInitialized;

        void Start()
        {

            var shipHUD = GetComponentInChildren<Game.UI.MiniGameHUD>();

            if (shipHUD == null)
            {
                return;
            }
            // TODO - Remove GameCanvas dependency

            /*if (vessel.VesselStatus.Player.GameCanvas != null)
            {
                // Disable the default HUD
                vessel.VesselStatus.Player.GameCanvas.MiniGameHUD.gameObject.SetActive(false);

                // Enable the modified HUD in the child
                shipHUD.gameObject.SetActive(true);

                // Assign the modified HUD to the vessel's player
                vessel.VesselStatus.Player.GameCanvas.MiniGameHUD = shipHUD;
            }*/

            shipHUD.gameObject.SetActive(true);
            onShipHUDInitialized.Raise(new ShipHUDData()
            {
                ShipHUD = shipHUD
            });
        }
    }
}
