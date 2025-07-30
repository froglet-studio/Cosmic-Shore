using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;

namespace CosmicShore
{
    /// <summary>
    /// Applies the CameraSettingsSO—including any ControlOverrideFlags—
    /// to the active ICameraController and CameraManager.
    /// </summary>
    public class ShipCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        public Transform FollowTarget;

        [Header("Per-Ship Camera Settings")]
        [SerializeField] private CameraSettingsSO settings;

        private IShip             ship;
        private CameraManager     cameraManager;
        private ICameraController cameraCtrl;

        /// <summary>
        /// Must be called when this ship becomes active (spawned/selected).
        /// </summary>
        public void Initialize(IShip ship)
        {
            this.ship = ship;
            cameraManager = CameraManager.Instance;
            cameraManager.Initialize(ship.ShipStatus);

            cameraCtrl = cameraManager.GetActiveController();
            if (cameraCtrl == null)
            {
                Debug.LogWarning("[ShipCameraCustomizer] No ICameraController available.");
                return;
            }
            if (settings == null)
            {
                Debug.LogWarning("[ShipCameraCustomizer] CameraSettingsSO is not assigned.");
                return;
            }
            
            cameraCtrl.ApplySettings(settings);
            ApplyControlOverrides();
        }

        public void Configure(ICameraController controller) { }

        private void ApplyControlOverrides()
        {
            var flags = settings.mode;

            if (flags.HasFlag(CameraMode.DynamicCamera))
            {
                cameraCtrl.SetCameraDistance(settings.dynamicMinDistance);
            }
            else
            {
                if (cameraCtrl is CustomCameraController cccFixed)
                {
                    cccFixed.SetFollowOffset(settings.followOffset);
                }
            }

            if (flags.HasFlag(CameraMode.FixedOffset))
            {
                if (cameraCtrl is CustomCameraController cccFO)
                {
                    cccFO.SetFollowOffset(settings.fixedOffsetPosition);
                }
            }
            
            if (flags.HasFlag(CameraMode.FollowTarget))
            {
                ship.Transform.position = settings.followTargetPosition;
            }
            cameraCtrl.SetFollowTarget(ship.Transform);

            if (flags.HasFlag(CameraMode.Orthographic) &&
                cameraCtrl is CustomCameraController cccOrtho)
            {
                cccOrtho.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
    }
}
