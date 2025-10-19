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
    public class VesselCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        [Header("Per-Vessel Camera Settings")]
        [SerializeField] private CameraSettingsSO settings;
        
        [SerializeField] ScriptableEventTransform OnInitializePlayerCamera;

        private IVessel vessel;
        private ICameraController _cameraCtrl;

        /// <summary>
        /// Must be called when this vessel becomes active (spawned/selected).
        /// </summary>
        public void Initialize(IVessel vessel)
        {
            this.vessel = vessel;
            OnInitializePlayerCamera.Raise(this.vessel.VesselStatus.CameraFollowTarget);
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
            
            _cameraCtrl.SetFollowTarget(vessel.Transform);

            if (flags.HasFlag(CameraMode.Orthographic) &&
                _cameraCtrl is CustomCameraController cccOrtho)
            {
                cccOrtho.SetOrthographic(true, settings.orthographicSize);
                Debug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
        
        public void RetargetAndApply(IVessel vessel)
        {
            this.vessel = vessel;

            OnInitializePlayerCamera?.Raise(vessel.VesselStatus.CameraFollowTarget);

            var active = CameraManager.Instance?.GetActiveController();
            if (active != null)
            {
                Configure(active);
            }
        }

    }
}
