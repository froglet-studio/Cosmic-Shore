using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages cameras for the Menu_Main scene using Cinemachine priority-based blending.
    ///
    /// Both menu orbit and freestyle follow cameras are CinemachineCamera instances
    /// living under <see cref="CameraManager"/>. Transitions happen by swapping their
    /// priorities — the CinemachineBrain on the main camera smoothly blends between them.
    ///
    /// Menu state: "CM Main Menu" vCam orbits the crystal at a configurable radius/height/speed.
    /// Freestyle state: "CM Freestyle" vCam follows the vessel with offset from <see cref="CameraSettingsSO"/>.
    ///
    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
    ///   - <c>OnClientReady</c>        → activate menu camera
    ///   - <c>OnEnterFreestyle</c>     → blend to freestyle camera
    ///   - <c>OnExitFreestyle</c>      → blend back to menu camera
    ///   - <c>OnCrystalSpawned</c>     → configure menu orbit target
    ///
    /// Place on the same GameObject as MainMenuController (the Game object in Menu_Main).
    /// Blend duration/curve is controlled by the CinemachineBrain's DefaultBlend setting.
    /// </summary>
    public class MainMenuCameraController : MonoBehaviour
    {
        [Header("Menu Camera Orbit")]
        [SerializeField, Tooltip("Orbit radius from crystal center.")]
        float _orbitRadius = 80f;

        [SerializeField, Tooltip("Camera height offset above the crystal.")]
        float _orbitHeight = 30f;

        [SerializeField, Tooltip("Orbit speed in degrees per second.")]
        float _orbitSpeed = 5f;

        [Header("SOAP Events")]
        [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
        MenuFreestyleEventsContainerSO _freestyleEvents;

        [SerializeField, Tooltip("Cell runtime data — provides crystal transform and spawn event.")]
        CellRuntimeDataSO _cellData;

        [Inject] GameDataSO _gameData;

        // Cached menu vCam hierarchy (lives on CameraManager)
        CinemachineCamera _menuVCam;
        CinemachineFollow _menuFollow;
        Transform _menuFollowTarget;
        RotateAroundOrigin _followTargetRotator;
        Transform _crystalTarget;

        // Freestyle vCam (created on CameraManager for Cinemachine priority blending)
        CinemachineCamera _freestyleVCam;
        CinemachineFollow _freestyleFollow;

        const int HighPriority = 20;
        const int LowPriority = 0;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            CacheMenuVCam();
            EnsureFreestyleVCam();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();

            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            // Ensure vCams don't bleed into subsequent scenes
            if (_menuVCam)
            {
                SetVCamPriority(_menuVCam, LowPriority);
                _menuVCam.gameObject.SetActive(false);
            }

            if (_freestyleVCam)
            {
                SetVCamPriority(_freestyleVCam, LowPriority);
                _freestyleVCam.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            UpdateMenuOrbit();
        }

        // ── Event Wiring ────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised += HandleMenuReady;

            _freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
            _cellData.OnCrystalSpawned.OnRaised += HandleCrystalSpawned;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            _freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
            _cellData.OnCrystalSpawned.OnRaised -= HandleCrystalSpawned;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady() => ActivateMenuCamera();
        void HandleEnterFreestyle() => ActivateGameplayCamera();
        void HandleExitFreestyle() => ActivateMenuCamera();
        void HandleCrystalSpawned() => SetMenuVCamTarget();

        // ── vCam Caching ──────────────────────────────────────────────

        void CacheMenuVCam()
        {
            if (!CameraManager.Instance) return;

            var cmTransform = CameraManager.Instance.transform.Find("CM Main Menu");
            if (!cmTransform) return;

            _menuVCam = cmTransform.GetComponent<CinemachineCamera>();
            _menuFollow = cmTransform.GetComponent<CinemachineFollow>();

            var followTransform = CameraManager.Instance.transform.Find("Main Menu Follow Target");
            if (followTransform)
            {
                _menuFollowTarget = followTransform;
                _followTargetRotator = followTransform.GetComponent<RotateAroundOrigin>();
            }
        }

        /// <summary>
        /// Creates or finds the freestyle CinemachineCamera used for smooth priority-based
        /// blending between menu orbit and vessel follow cameras.
        /// If a "CM Freestyle" child already exists on CameraManager it is reused;
        /// otherwise one is created at runtime with CinemachineFollow + CinemachineRotationComposer.
        /// </summary>
        void EnsureFreestyleVCam()
        {
            if (_freestyleVCam) return;
            if (!CameraManager.Instance) return;

            var parent = CameraManager.Instance.transform;
            var existing = parent.Find("CM Freestyle");

            if (existing)
            {
                _freestyleVCam = existing.GetComponent<CinemachineCamera>();
                _freestyleFollow = existing.GetComponent<CinemachineFollow>();

                // Strip CinemachineRotationComposer if present — LockToTarget rotation
                // tracking is sufficient and keeps position/rotation damping in sync.
                var composer = existing.GetComponent<CinemachineRotationComposer>();
                if (composer) Destroy(composer);
            }
            else
            {
                var go = new GameObject("CM Freestyle");
                go.transform.SetParent(parent, false);

                _freestyleVCam = go.AddComponent<CinemachineCamera>();
                _freestyleFollow = go.AddComponent<CinemachineFollow>();

                // LockToTarget interprets FollowOffset in the target's local space
                // so the camera stays behind the vessel as it rotates — matching
                // CustomCameraController's _followTarget.rotation * _followOffset.
                // No CinemachineRotationComposer — LockToTarget already tracks the
                // vessel's rotation via RotationDamping, keeping camera Up = vessel Up
                // and camera forward = vessel forward.
                var tracker = _freestyleFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTarget;
                _freestyleFollow.TrackerSettings = tracker;
            }

            SetVCamPriority(_freestyleVCam, LowPriority);
        }

        // ── Menu Camera Orbit ───────────────────────────────────────────

        void SetMenuVCamTarget()
        {
            if (!_menuVCam) return;

            var crystalTransform = _cellData.CrystalTransform;
            if (!crystalTransform) return;

            _crystalTarget = crystalTransform;

            // Position follow target at orbit radius from crystal
            if (_menuFollowTarget)
            {
                _menuFollowTarget.position = crystalTransform.position + Vector3.back * _orbitRadius;

                // Disable default RotateAroundOrigin — it orbits world origin, not the crystal
                if (_followTargetRotator) _followTargetRotator.enabled = false;
            }

            // TrackingTarget = orbiting follow target (for camera positioning)
            // LookAtTarget = crystal (for camera aiming via CinemachineRotationComposer)
            var target = _menuVCam.Target;
            target.TrackingTarget = _menuFollowTarget ? _menuFollowTarget : crystalTransform;
            target.LookAtTarget = crystalTransform;
            target.CustomLookAtTarget = true;
            _menuVCam.Target = target;

            // CinemachineFollow offset provides height above the orbit path
            if (_menuFollow)
                _menuFollow.FollowOffset = new Vector3(0, _orbitHeight, 0);
        }

        void UpdateMenuOrbit()
        {
            if (!_crystalTarget || !_menuFollowTarget) return;

            var pivot = _crystalTarget.position;
            var offset = _menuFollowTarget.position - pivot;
            offset = Quaternion.Euler(0, _orbitSpeed * Time.deltaTime, 0) * offset;
            _menuFollowTarget.position = pivot + offset;
        }

        // ── Camera Switching ────────────────────────────────────────────

        /// <summary>
        /// Activates the CM Main Menu Cinemachine camera for menu state.
        /// Deactivates CameraManager gameplay cameras (CM PlayerCam, etc.) and raises
        /// the menu vCam's priority so the CinemachineBrain smoothly blends to it
        /// from whatever vCam was previously active.
        /// </summary>
        void ActivateMenuCamera()
        {
            if (!CameraManager.Instance) return;

            // Deactivate non-Cinemachine gameplay cameras (CM PlayerCam, CM DeathCam, CM EndCam)
            // to prevent their Camera components from rendering alongside the CinemachineBrain.
            CameraManager.Instance.DeactivateAllCameras();

            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
                SetVCamPriority(_menuVCam, HighPriority);
            }

            // Keep freestyle vCam active so the CinemachineBrain can blend FROM it,
            // but drop its priority so the menu vCam wins.
            if (_freestyleVCam)
                SetVCamPriority(_freestyleVCam, LowPriority);
        }

        /// <summary>
        /// Activates a Cinemachine freestyle vCam that follows the vessel, using
        /// priority-based blending for a smooth transition from the menu orbit camera.
        /// The freestyle vCam's offset is configured from the vessel's <see cref="CameraSettingsSO"/>
        /// via <see cref="VesselCameraCustomizer"/>. Does NOT activate the CustomCameraController
        /// (CM PlayerCam) — the entire transition stays within the Cinemachine pipeline.
        /// </summary>
        void ActivateGameplayCamera()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            EnsureFreestyleVCam();
            if (!_freestyleVCam) return;

            // Configure freestyle vCam to follow the vessel
            var target = _freestyleVCam.Target;
            target.TrackingTarget = followTarget;
            _freestyleVCam.Target = target;

            // Apply vessel camera settings — mirrors CustomCameraController.ApplySettings()
            // so the freestyle vCam lands at the same position/orientation the gameplay
            // camera would use.
            if (_freestyleFollow)
            {
                var customizer = player.Vessel.VesselStatus.VesselCameraCustomizer;
                if (customizer != null && customizer.Settings != null)
                {
                    var settings = customizer.Settings;

                    // Offset: DynamicCamera uses X/Y from followOffset + Z from dynamicMinDistance.
                    // FixedCamera uses the full followOffset vector directly.
                    _freestyleFollow.FollowOffset = settings.mode == CameraMode.DynamicCamera
                        ? new Vector3(settings.followOffset.x, settings.followOffset.y, settings.dynamicMinDistance)
                        : settings.followOffset;

                    // Mirror CustomCameraController.ApplySettings() damping behavior:
                    // FixedCamera → snap (zero damping, disableRotationLerp = true)
                    // DynamicCamera → smooth (followSmoothTime for position and rotation)
                    var damping = settings.mode == CameraMode.DynamicCamera
                        ? new Vector3(settings.followSmoothTime, settings.followSmoothTime, settings.followSmoothTime)
                        : Vector3.zero;
                    var tracker = _freestyleFollow.TrackerSettings;
                    tracker.BindingMode = BindingMode.LockToTarget;
                    tracker.PositionDamping = damping;
                    tracker.RotationDamping = damping;
                    _freestyleFollow.TrackerSettings = tracker;
                }
            }

            _freestyleVCam.gameObject.SetActive(true);

            // Priority switch — CinemachineBrain smoothly blends from menu orbit to vessel follow
            SetVCamPriority(_freestyleVCam, HighPriority);
            if (_menuVCam) SetVCamPriority(_menuVCam, LowPriority);
        }

        static void SetVCamPriority(CinemachineCamera cam, int value)
        {
            var p = cam.Priority;
            p.Enabled = true;
            p.Value = value;
            cam.Priority = p;
        }
    }
}
