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
            
            Debug.Log(
                $"[Customizer] SO={settings.name}  " +
                $"Flags={settings.controlOverrides}  " +
                $"Close={settings.closeCamDistance}  " +
                $"Far={settings.farCamDistance}  " +
                $"Offset={settings.followOffset}",
                this
            );

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
            Vector3 desiredOffset = settings.followOffset; // default
            Debug.Log($"[Overrides] flags = {flags}", this);
            // A) Close/Far
            if (flags.HasFlag(ControlOverrideFlags.CloseCam) ||
                flags.HasFlag(ControlOverrideFlags.FarCam))
            {
                float targetDist = flags.HasFlag(ControlOverrideFlags.FarCam)
                    ? settings.farCamDistance
                    : settings.closeCamDistance;
                // preserve X/Y from SO and invert Z:
                desiredOffset = new Vector3(
                    settings.followOffset.x,
                    settings.followOffset.y,
                    -targetDist
                );
                Debug.Log($"[ShipCameraCustomizer] ZoomOverride → offset {desiredOffset}");
            }

            // B) Follow-Target
            if (flags.HasFlag(ControlOverrideFlags.FollowTarget))
            {
                ship.Transform.position = settings.followTargetPosition;
                cameraCtrl.SetFollowTarget(ship.Transform);
                Debug.Log($"[ShipCameraCustomizer] FollowTarget override → pos {settings.followTargetPosition}");
            }
            else
            {
                cameraCtrl.SetFollowTarget(ship.Transform);
            }

            // C) Fixed-Offset
            if (flags.HasFlag(ControlOverrideFlags.FixedOffset))
            {
                desiredOffset = settings.fixedOffsetPosition;
                Debug.Log($"[ShipCameraCustomizer] FixedOffset override → pos {desiredOffset}");
            }
            Debug.Log($"[Overrides] calling SetOffsetPosition({desiredOffset})", this);
            // D) Apply offset **only** via CameraManager (no direct SetFollowOffset)
            cameraManager.SetOffsetPosition(desiredOffset);

            // E) Freeze Manager so it doesn't overwrite
            cameraManager.FreezeRuntimeOffset = true;

            // F) Orthographic override (cast to concrete)
            if (flags.HasFlag(ControlOverrideFlags.Orthographic) &&
                cameraCtrl is CustomCameraController ccc)
            {
                ccc.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }

            // Final debug
            Debug.Log($"[ShipCameraCustomizer] Final offset: {desiredOffset}");
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

