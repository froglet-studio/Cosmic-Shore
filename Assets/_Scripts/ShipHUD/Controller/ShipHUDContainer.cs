using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ShipHUDContainer : MonoBehaviour
    {
        [System.Serializable]
        public struct HUDPrefabVariant
        {
            public ShipClassType shipType;
            public ShipHUDView prefab;
        }

        [Header("Prefabs & Mount")]
        [SerializeField] HUDPrefabVariant[] hudVariants;
        [SerializeField] RectTransform contentTransform;

        ShipHUDView   _activeInstance;
        IShipHUDView _activeHUDView;

        public void InitializeView(IShipStatus shipStatus, ShipClassType shipClass)
        {
            if (shipStatus == null || contentTransform == null) return;

            if (shipStatus.AutoPilotEnabled)
            {
                TearDownActive();
                return;
            }

            var variant = hudVariants.FirstOrDefault(v => v.shipType == shipClass);
            if (variant.prefab == null)
            {
                Debug.LogWarning($"[ShipHUDContainer] No HUD prefab for {shipClass}");
                TearDownActive();
                return;
            }
            TearDownActive();

            _activeInstance = Instantiate(variant.prefab, contentTransform);
            _activeInstance.gameObject.SetActive(true);

            _activeHUDView = _activeInstance;
            if (_activeHUDView == null)
            {
                Debug.LogWarning($"[ShipHUDContainer] Spawned HUD for {shipClass} has no IShipHUDView.");
                return;
            }
            shipStatus.ShipHudView = _activeHUDView as ShipHUDView;
            
            var controller = shipStatus.ShipHUDController;
            var baseView = _activeHUDView as ShipHUDView;
            if (baseView == null)
                Debug.LogWarning($"[ShipHUDContainer] IShipHUDView is not an R_ShipHUDView; controllers may expect that type.");

            controller.Initialize(shipStatus, baseView);
           
        }

        private void TearDownActive()
        {
            if (_activeInstance != null)
            {
                Destroy(_activeInstance);
                _activeInstance = null;
            }
            _activeHUDView = null;
        }

        /// <summary>Returns the currently active HUD view (if any).</summary>
        public IShipHUDView GetActiveView() => _activeHUDView;
    }
}
