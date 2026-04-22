using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies the CameraSettingsSO—including any ControlOverrideFlags—
    /// to the active ICameraController and CameraManager.
    /// </summary>
    public class VesselCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        [Header("Per-Vessel Camera Settings")]
        [SerializeField] private CameraSettingsSO settings;

        /// <summary>The per-vessel camera configuration asset.</summary>
        public CameraSettingsSO Settings => settings;
        
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
                CSDebug.Log($"[ShipCameraCustomizer] Orthographic override → size {settings.orthographicSize}");
            }
        }
        
        public void RetargetAndApply(IVessel vessel)
        {
            Initialize(vessel);
            var active = CameraManager.Instance.GetActiveController();
            if (active != null)
            {
                Configure(active);
            }
        }
    }
}
