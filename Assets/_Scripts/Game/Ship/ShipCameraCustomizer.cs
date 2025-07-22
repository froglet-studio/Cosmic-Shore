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

            Debug.Log($"[ShipCameraCustomizer] Initializing camera for “{ship.ShipStatus.Name}”");

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

            // 1) Apply common follow/rotation/update settings
            cameraCtrl.ApplySettings(settings);

            // 2) Apply each flagged override
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

            // A) Close/Far zoom overrides
            if (flags.HasFlag(ControlOverrideFlags.CloseCam) ||
                flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                float targetDist = flags.HasFlag(ControlOverrideFlags.FarCam)
                    ? settings.farCamDistance
                    : settings.closeCamDistance;

                // world-space Z offset
                cameraManager.SetOffsetPosition(new Vector3(0f, 0f, targetDist));
                Debug.Log($"[ShipCameraCustomizer] Applied ZoomOverride: {targetDist}");
            }

            // B) Follow-target reposition override
            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                // move the ship’s pivot (or a child) to the SO position
                ship.Transform.position = settings.followTargetPosition;
                cameraCtrl.SetFollowTarget(ship.Transform);
                Debug.Log($"[ShipCameraCustomizer] FollowTarget override → pos {settings.followTargetPosition}");
            }
            else
            {
                // default follow
                cameraCtrl.SetFollowTarget(ship.Transform);
            }

            // C) Fixed-offset override (locks camera at a world position)
            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                cameraManager.SetFixedFollowOffset(settings.fixedOffsetPosition);
                Debug.Log($"[ShipCameraCustomizer] FixedOffset override → pos {settings.fixedOffsetPosition}");
            }

            // D) Orthographic override
            if (flags.HasFlag(ControlOverrideFlags.Orthographic))
            {
                // CustomCameraController exposes SetOrthographic
                if (cameraCtrl is CustomCameraController ccc)
                {
                    ccc.SetOrthographic(true, settings.orthographicSize);
                    Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
                }
                else
                {
                    Debug.LogWarning("[ShipCameraCustomizer] Cannot apply Orthographic override to current controller.");
                }
            }

//             // E) Final debug dump
//             Debug.Log($"""
//                 [ShipCameraCustomizer] “{ship.ShipStatus.Name}” settings summary:
//                   • followOffset:        {settings.followOffset}
//                   • followSmoothTime:    {settings.followSmoothTime}
//                   • rotationSmoothTime:  {settings.rotationSmoothTime}
//                   • disableRotationLerp: {settings.disableRotationLerp}
//                   • useFixedUpdate:      {settings.useFixedUpdate}
//
//                   • ZoomOverrides:       Close={settings.closeCamDistance}, Far={settings.farCamDistance}
//                   • FollowTargetPos?:    {(flags.HasFlag(ControlOverrideFlags.FollowTarget) ? settings.followTargetPosition.ToString() : "none")}
//                   • FixedOffsetPos?:     {(flags.HasFlag(ControlOverrideFlags.FixedOffset) ? settings.fixedOffsetPosition.ToString() : "none")}
//                   • OrthographicSize?:   {(flags.HasFlag(ControlOverrideFlags.Orthographic) ? settings.orthographicSize.ToString() : "none")}
//                 """);
        }
    }
}
