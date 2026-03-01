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
    /// Menu state: "CM Main Menu" Cinemachine vCam orbits the crystal at a
    /// configurable radius/height/speed.
    /// Freestyle state: <see cref="CameraManager.SetupGamePlayCameras"/> activates
    /// the proven <see cref="CustomCameraController"/> ("CM PlayerCam") to follow
    /// the vessel — the same pipeline used by all gameplay scenes.
    ///
    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
    ///   - <c>OnClientReady</c>        → activate menu camera
    ///   - <c>OnEnterFreestyle</c>     → switch to gameplay camera (CustomCameraController)
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

            if (_menuVCam)
                _menuVCam.gameObject.SetActive(false);
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
        /// Activates the Cinemachine menu orbit camera and deactivates gameplay cameras.
        /// Uses <see cref="CameraManager.SetMainMenuCameraActive"/> to deactivate all
        /// CustomCameraController instances (CM PlayerCam, CM DeathCam, CM EndCam) and
        /// re-enable the Cinemachine menu vCam.
        /// </summary>
        void ActivateMenuCamera()
        {
            if (!CameraManager.Instance) return;

            CameraManager.Instance.SetMainMenuCameraActive();

            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Activates the <see cref="CustomCameraController"/> ("CM PlayerCam") via
        /// <see cref="CameraManager.SetupGamePlayCameras"/> to follow the vessel.
        /// This is the same proven camera pipeline used by all gameplay scenes on the
        /// development branch — it applies the vessel's <see cref="CameraSettingsSO"/>,
        /// sets the follow target, and snaps the camera to the correct initial position.
        /// </summary>
        void ActivateGameplayCamera()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            // Deactivate the Cinemachine menu camera so it doesn't fight
            // with the CustomCameraController's Camera component.
            if (_menuVCam)
                _menuVCam.gameObject.SetActive(false);

            // Use the same camera setup path as gameplay scenes:
            // sets follow target, applies CameraSettingsSO via VesselCameraCustomizer,
            // activates CM PlayerCam, and snaps to the correct position.
            CameraManager.Instance.SetupGamePlayCameras(followTarget);
        }
    }
}
