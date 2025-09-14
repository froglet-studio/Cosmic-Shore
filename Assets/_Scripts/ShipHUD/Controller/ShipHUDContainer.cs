using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    public class ShipHUDContainer : MonoBehaviour
    {
        [System.Serializable]
        public struct HUDPrefabVariant
        {
            [FormerlySerializedAs("shipType")] public VesselClassType vesselType;
            public ShipHUDView prefab;
        }

        [Header("Prefabs & Mount")]
        [SerializeField] HUDPrefabVariant[] hudVariants;
        [SerializeField] RectTransform contentTransform;

        ShipHUDView   _activeInstance;
        IShipHUDView _activeHUDView;

        public void InitializeView(IVesselStatus vesselStatus, VesselClassType vesselClass)
        {
            if (vesselStatus == null || contentTransform == null) return;

            if (vesselStatus.AutoPilotEnabled)
            {
                TearDownActive();
                return;
            }

            var variant = hudVariants.FirstOrDefault(v => v.vesselType == vesselClass);
            if (variant.prefab == null)
            {
                Debug.LogWarning($"[ShipHUDContainer] No HUD prefab for {vesselClass}");
                TearDownActive();
                return;
            }
            TearDownActive();

            _activeInstance = Instantiate(variant.prefab, contentTransform);
            _activeInstance.gameObject.SetActive(true);

            // _activeHUDView = _activeInstance;
            if (_activeHUDView == null)
            {
                Debug.LogWarning($"[ShipHUDContainer] Spawned HUD for {vesselClass} has no IShipHUDView.");
                return;
            }
            vesselStatus.ShipHudView = _activeHUDView as ShipHUDView;
            
            var controller = vesselStatus.ShipHUDController;
            var baseView = _activeHUDView as ShipHUDView;
            if (baseView == null)
                Debug.LogWarning($"[ShipHUDContainer] IShipHUDView is not an R_ShipHUDView; controllers may expect that type.");

            controller.Initialize(vesselStatus, baseView);
           
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
