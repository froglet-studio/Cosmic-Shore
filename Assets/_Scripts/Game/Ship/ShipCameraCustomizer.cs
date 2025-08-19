using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utilities;


namespace CosmicShore
{
    /// <summary>
    /// Applies the CameraSettingsSO—including any ControlOverrideFlags—
    /// to the active ICameraController and CameraManager.
    /// </summary>
    public class ShipCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        [Header("Per-Ship Camera Settings")]
        [SerializeField] private CameraSettingsSO settings;
        
        [SerializeField] ScriptableEventTransform OnInitializePlayerCamera;

        private IShip _ship;
        private ICameraController _cameraCtrl;

        /// <summary>
        /// Must be called when this ship becomes active (spawned/selected).
        /// </summary>
        public void Initialize(IShip ship)
        {
            _ship = ship;
            OnInitializePlayerCamera.Raise(_ship.ShipStatus.FollowTarget);
        }

        public void Configure(ICameraController controller)
        {
            _cameraCtrl = controller;
            controller.ApplySettings(settings);
            ApplyControlOverrides();
        }

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
