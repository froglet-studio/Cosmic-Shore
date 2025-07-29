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

        private IShip               ship;
        private CameraManager       cameraManager;
        private ICameraController   cameraCtrl;

        /// <summary>
        /// Must be called when this ship becomes active (spawned/selected).
        /// </summary>
        public void Initialize(IShip ship)
        {
            this.ship = ship;
            cameraManager = CameraManager.Instance;
            cameraManager.Initialize(ship.ShipStatus);

            // Here, use playerCamera or ask CameraManager for the correct controller directly.
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
            var flags = settings.controlOverrides;
            
            // 1. Set camera distance (CloseCam/FarCam logic)
            if (flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                cameraCtrl.SetCameraDistance(settings.farCamDistance);
            }
            else
            {
                cameraCtrl.SetCameraDistance(settings.closeCamDistance);
            }

            // 2. FixedOffset (rare, but if you need to support, add to controller API)
            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                // If you want to allow full custom world-space offset (not recommended for most gameplay cams)
                if (cameraCtrl is CustomCameraController ccc)
                    ccc.SetFollowOffset(settings.fixedOffsetPosition);
            }

            // 3. FollowTarget (move ship then set target)
            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                ship.Transform.position = settings.followTargetPosition;
                cameraCtrl.SetFollowTarget(ship.Transform);
            }
            else
            {
                cameraCtrl.SetFollowTarget(ship.Transform);
            }

            // 4. Orthographic
            if (flags.HasFlag(ControlOverrideFlags.Orthographic) &&
                cameraCtrl is CustomCameraController ccc2)
            {
                ccc2.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
    } 
}
