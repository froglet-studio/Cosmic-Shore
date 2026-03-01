using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Cinemachine;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages cameras for the Menu_Main scene.
    ///
    /// Menu state uses the "CM Main Menu" Cinemachine vCam on <see cref="CameraManager"/>,
    /// orbiting the crystal at a configurable radius/height/speed.
    ///
    /// Freestyle state delegates to <see cref="CameraManager.SetupGamePlayCameras"/>,
    /// which activates the per-vessel <see cref="CustomCameraController"/> (CM PlayerCam)
    /// with the vessel's own <see cref="CameraSettingsSO"/> applied via <see cref="VesselCameraCustomizer"/>.
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

        const int HighPriority = 20;
        const int LowPriority = 0;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            CacheMenuVCam();
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
        /// Deactivates all CameraManager gameplay cameras and enables the menu
        /// vCam with high priority so the CinemachineBrain blends to it.
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
        }

        /// <summary>
        /// Activates the per-vessel gameplay camera (CM PlayerCam) for freestyle mode.
        /// Delegates to <see cref="CameraManager.SetupGamePlayCameras"/> which applies
        /// the vessel's <see cref="CameraSettingsSO"/> via <see cref="VesselCameraCustomizer"/>.
        /// </summary>
        void ActivateGameplayCamera()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            if (_menuVCam)
                _menuVCam.gameObject.SetActive(false);

            CameraManager.Instance.SetupGamePlayCameras(followTarget);
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
