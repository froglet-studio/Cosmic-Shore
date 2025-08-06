using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using UnityEngine.Serialization;

namespace CosmicShore
{
    /// <summary>
    /// Applies the CameraSettingsSO—including any ControlOverrideFlags—
    /// to the active ICameraController and CameraManager.
    /// </summary>
    public class ShipCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        [HideInInspector]public Transform followTarget;

        [Header("Per-Ship Camera Settings")]
        [SerializeField] private CameraSettingsSO settings;

        private IShip _ship;
        private CameraManager _cameraManager;
        private ICameraController _cameraCtrl;

        /// <summary>
        /// Must be called when this ship becomes active (spawned/selected).
        /// </summary>
        public void Initialize(IShip ship)
        {
            this._ship = ship;
            _cameraManager = CameraManager.Instance;
            _cameraManager.Initialize(ship.ShipStatus);

            _cameraCtrl = _cameraManager.GetActiveController();
            if (_cameraCtrl == null)
            {
                Debug.LogWarning("[ShipCameraCustomizer] No ICameraController available.");
                return;
            }
            if (settings == null)
            {
                Debug.LogWarning("[ShipCameraCustomizer] CameraSettingsSO is not assigned.");
                return;
            }
            
            _cameraCtrl.ApplySettings(settings);
            ApplyControlOverrides();
        }

        public void Configure(ICameraController controller) { }

        private void ApplyControlOverrides()
        {
            var flags = settings.mode;

            if (flags.HasFlag(CameraMode.DynamicCamera))
            {
                _cameraCtrl.SetCameraDistance(settings.dynamicMinDistance);
            }
            else
            {
                if (_cameraCtrl is CustomCameraController cccFixed)
                {
                    cccFixed.SetFollowOffset(settings.followOffset);
                }
            }
            
            _cameraCtrl.SetFollowTarget(_ship.Transform);

            if (flags.HasFlag(CameraMode.Orthographic) &&
                _cameraCtrl is CustomCameraController cccOrtho)
            {
                cccOrtho.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
    }
}
