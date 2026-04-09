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
            // Search includes inactive so the prefab can ship with the
            // MiniGameHUD disabled, avoiding its Start() lifecycle.
            var shipHUD = GetComponentInChildren<MiniGameHUD>(true);

            if (shipHUD == null)
                return;

            // Temporarily activate so the scene-level handler
            // (MenuMiniGameHUD / MiniGameHUD) can discover and reparent
            // the direct children via the SOAP event.
            shipHUD.gameObject.SetActive(true);
            onShipHUDInitialized.Raise(new ShipHUDData()
            {
                ShipHUD = shipHUD
            });

            // Deactivate the vessel's embedded MiniGameHUD now that its
            // children have been reparented. This prevents its Start()
            // from firing (which would subscribe to SOAP events and call
            // Show(), briefly making the HUD visible on the vessel's Canvas).
            shipHUD.gameObject.SetActive(false);
        }
    }
}
