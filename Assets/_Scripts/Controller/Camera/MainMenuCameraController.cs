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
    /// Manages all Cinemachine cameras for the Menu_Main scene.
    ///
    /// Owns two virtual cameras:
    ///   1. <b>Menu vCam</b> — pre-existing "CM Main Menu" on <see cref="CameraManager"/>.
    ///      Orbits the crystal at a configurable radius/height/speed.
    ///   2. <b>Gameplay vCam</b> — dynamically created "CM Menu Gameplay".
    ///      Follows the local player's vessel in freestyle mode.
    ///
    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
    ///   - <c>OnClientReady</c>        → activate menu camera
    ///   - <c>OnEnterFreestyle</c>     → switch to gameplay camera
    ///   - <c>OnExitFreestyle</c>      → switch back to menu camera
    ///   - <c>OnCrystalSpawned</c>     → configure menu orbit target
    ///
    /// Place on the same GameObject as MainMenuController (the Game object in Menu_Main).
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

        [Header("Freestyle Camera")]
        [SerializeField, Tooltip("Follow offset for the freestyle Cinemachine vCam.")]
        Vector3 _gameplayFollowOffset = new(0, 5, -25);

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

        // Dynamically created freestyle vCam (lives on this GameObject)
        CinemachineCamera _gameplayVCam;

        const int HighPriority = 20;
        const int LowPriority = 0;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            CacheMenuVCam();
            CreateGameplayVCam();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();

            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            // Ensure menu vCam doesn't bleed into subsequent scenes
            if (_menuVCam)
            {
                SetVCamPriority(_menuVCam, LowPriority);
                _menuVCam.gameObject.SetActive(false);
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

        // ── vCam Caching & Creation ─────────────────────────────────────

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

        void CreateGameplayVCam()
        {
            var go = new GameObject("CM Menu Gameplay");
            go.transform.SetParent(transform);

            // Add pipeline components before CinemachineCamera so it discovers them on enable
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = _gameplayFollowOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = new Vector3(1, 1, 1);
            follow.TrackerSettings = tracker;

            go.AddComponent<CinemachineRotationComposer>();

            _gameplayVCam = go.AddComponent<CinemachineCamera>();
            SetVCamPriority(_gameplayVCam, LowPriority);

            var lens = _gameplayVCam.Lens;
            lens.FieldOfView = 60;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = 12000;
            _gameplayVCam.Lens = lens;
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
        /// Deactivates all CameraManager gameplay cameras and raises the menu
        /// vCam's priority so the CinemachineBrain blends to it smoothly.
        /// </summary>
        void ActivateMenuCamera()
        {
            if (!CameraManager.Instance) return;
            CameraManager.Instance.DeactivateAllCameras();

            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
                SetVCamPriority(_menuVCam, HighPriority);
            }

            if (_gameplayVCam)
                SetVCamPriority(_gameplayVCam, LowPriority);
        }

        /// <summary>
        /// Activates the gameplay Cinemachine vCam for freestyle mode.
        /// Raises the gameplay vCam's priority so the CinemachineBrain blends
        /// from the menu orbit camera to the vessel follow camera smoothly.
        /// </summary>
        void ActivateGameplayCamera()
        {
            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            if (_gameplayVCam)
            {
                var target = _gameplayVCam.Target;
                target.TrackingTarget = followTarget;
                target.CustomLookAtTarget = false;
                _gameplayVCam.Target = target;

                SetVCamPriority(_gameplayVCam, HighPriority);
            }

            if (_menuVCam)
                SetVCamPriority(_menuVCam, LowPriority);
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
