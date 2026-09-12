using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies the CameraSettingsSO-including any ControlOverrideFlags-
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

        /// <summary>
        /// Tells this customizer which vessel it belongs to WITHOUT announcing a player camera.
        /// <para><see cref="Initialize"/> is the local pilot's path: it also raises
        /// <c>OnInitializePlayerCamera</c>, which is how the gameplay rig latches onto the ship
        /// you are flying. A machine with no local pilot - a SPECTATOR - still needs a camera
        /// pointed at somebody's ship, and it chooses which one itself; it must not fire the
        /// "this is the player's vessel" announcement to do it.</para>
        /// </summary>
        public void Adopt(IVessel vessel) => this.vessel = vessel;

        public void Configure(ICameraController controller)
        {
            if (controller == null) return;
            if (settings == null)
            {
                // Fail loud, but do not take the caller down with it: Configure runs inside the
                // roster's pair loop, where a throw costs every pilot after this one.
                CSDebug.LogError($"[VesselCameraCustomizer] No CameraSettingsSO on {name}. " +
                                 "The camera keeps whatever settings it had.");
                return;
            }

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
            
            // A customizer that was never handed its vessel still has to be configurable: the
            // reference is only set for the LOCAL pilot (VesselController.Initialize gates it on
            // IsLocalUser), so on a machine with no local pilot - a spectator - every vessel in
            // the match carries an un-initialized one. Dereferencing it there threw an NRE out of
            // Configure, up through vessel.Initialize and out of ClientPlayerVesselInitializer's
            // pair loop, so the watched pilot was never added to the roster at all. This component
            // lives on the vessel root (VesselStatus [RequireComponent]s it), so `transform` IS
            // that vessel - the fallback is the same object, not a guess.
            var followTarget = vessel?.Transform != null ? vessel.Transform : transform;
            _cameraCtrl.SetFollowTarget(followTarget);

            if (flags.HasFlag(CameraMode.Orthographic) &&
                _cameraCtrl is CustomCameraController cccOrtho)
            {
                cccOrtho.SetOrthographic(true, settings.orthographicSize);
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
