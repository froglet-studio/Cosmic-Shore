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
            cameraCtrl     = cameraManager.GetActiveController();

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

        /// <summary>
        /// ICameraConfigurator implementation remains empty because
        /// we drive everything from Initialize().
        /// </summary>
        public void Configure(ICameraController controller) { }

        private void ApplyControlOverrides()
        {
            var flags = settings.controlOverrides;
            Vector3 desiredOffset = settings.followOffset; 
         
            if (flags.HasFlag(ControlOverrideFlags.CloseCam) ||
                flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                float targetDist = flags.HasFlag(ControlOverrideFlags.FarCam)
                    ? settings.farCamDistance
                    : settings.closeCamDistance;
     
                desiredOffset = new Vector3(
                    settings.followOffset.x,
                    settings.followOffset.y,
                    -targetDist
                );
            }

            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                ship.Transform.position = settings.followTargetPosition;
                cameraCtrl.SetFollowTarget(ship.Transform);
            }
            else
            {
                cameraCtrl.SetFollowTarget(ship.Transform);
            }

            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                desiredOffset = settings.fixedOffsetPosition;
            }
            cameraManager.SetOffsetPosition(desiredOffset);
            cameraManager.FreezeRuntimeOffset = true;

            if (flags.HasFlag(ControlOverrideFlags.Orthographic) &&
                cameraCtrl is CustomCameraController ccc)
            {
                ccc.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
    } 
}

