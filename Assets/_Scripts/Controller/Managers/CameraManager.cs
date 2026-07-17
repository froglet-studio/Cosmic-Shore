using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    public class CameraManager : Singleton<CameraManager>
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField]
        SceneNameListSO _sceneNameList;

        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        [SerializeField] private ScriptableEventNoParam _onReturnToMainMenu;
        [SerializeField] private ScriptableEventTransform _onInitializePlayerCamera;

        // TODO - Need to have a game over event, to activate the end camera
        // += SetEndCameraActive
        // [SerializeField] private ScriptableEventNoParam _onGameOver;

        private ICameraController _playerCamera;
        private ICameraController _deathCamera;
        private ICameraController _activeController;

        [SerializeField] private CustomCameraController endCamera;
        [SerializeField] private CinemachineCamera mainMenuCamera;
        [SerializeField] private Transform endCameraFollowTarget;
        [SerializeField] private Transform endCameraLookAtTarget;
        [SerializeField] private float startTransitionDistance = 40f;

        private Transform _playerFollowTarget;
        private const int ActivePriority = 10;

        public Transform PlayerFollowTarget
        {
            get => _playerFollowTarget;
            set => _playerFollowTarget = value;
        }

        private Camera _vCam;
        private IVesselStatus vesselStatus;

        public override void Awake()
        {
            base.Awake();
            _playerCamera = GetOrFindCameraController("CM PlayerCam");
            _deathCamera = GetOrFindCameraController("CM DeathCam");
            endCamera = GetOrFindCameraController("CM EndCam") as CustomCameraController;
        }

        private void OnEnable()
        {
            _onReturnToMainMenu.OnRaised += OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised += SetupGamePlayCameras;

            // Keep FOV + post-process AA on the managed cameras in sync with the settings panel,
            // live. The cameras are children of this manager (not spawned with the vessel), so this
            // is the spawn-proof place to apply - no per-camera scene reference needed.
            DisplayGraphicsSettings.OnFieldOfViewChanged += HandleCameraGraphicsChanged;
            DisplayGraphicsSettings.OnAnySettingChanged += HandleCameraGraphicsChanged;
        }

        void OnDisable()
        {
            _onReturnToMainMenu.OnRaised -= OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised -= SetupGamePlayCameras;

            DisplayGraphicsSettings.OnFieldOfViewChanged -= HandleCameraGraphicsChanged;
            DisplayGraphicsSettings.OnAnySettingChanged -= HandleCameraGraphicsChanged;
        }

        void HandleCameraGraphicsChanged(float _) => ApplyCameraGraphicsSettings();
        void HandleCameraGraphicsChanged(GraphicsSettingsData _) => ApplyCameraGraphicsSettings();

        /// <summary>
        /// Pushes the saved Field-of-View and post-process AA (FXAA/SMAA/TAA) onto every managed
        /// camera. MSAA is global on the URP asset (handled by <see cref="DisplayGraphicsSettings"/>),
        /// so this only sets FOV + the per-camera post-AA mode. Null-safe and called on every camera
        /// setup, so a vessel that spawns later still gets the right look.
        /// </summary>
        public void ApplyCameraGraphicsSettings()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            var d = s.Current;
            ApplyToCamera((_playerCamera as CustomCameraController)?.Camera, d);
            ApplyToCamera((_deathCamera as CustomCameraController)?.Camera, d);
            ApplyToCamera(endCamera != null ? endCamera.Camera : null, d);
        }

        static void ApplyToCamera(Camera cam, GraphicsSettingsData d)
        {
            if (cam == null) return;
            if (!cam.orthographic) cam.fieldOfView = d.FieldOfView;
            GraphicsSettingsApplier.ApplyCameraAntiAliasing(cam, d.AntiAliasing);
        }

        void Start()
        {
            _vCam = (_playerCamera as CustomCameraController)?.Camera;
            InitializeSceneCamera();
        }

        private void InitializeSceneCamera()
        {
            var activeScene = SceneManager.GetActiveScene().name;

            if (activeScene == _sceneNameList.MainMenuScene)
            {
                OnEnteredMainMenu();
            }
        }

        private ICameraController GetOrFindCameraController(string name)
        {
            Transform t = transform.Find(name);
            if (t)
            {
                var ctrl = t.GetComponent<ICameraController>();
                if (ctrl == null)
                {
                    ctrl = t.gameObject.AddComponent<CustomCameraController>();
                }
                return ctrl;
            }
            CSDebug.LogWarning($"[CameraManager] Could not find camera controller: {name}");
            return null;
        }

        public Transform GetCloseCamera() => (_playerCamera as CustomCameraController)?.transform;

        void OnEnteredMainMenu()
        {
            SetMainMenuCameraActive();
            _themeManagerData.SetBackgroundColor(Camera.main);
        }

        public void SetupGamePlayCameras(Transform followTarget)
        {
            if(!gameObject.activeInHierarchy) gameObject.SetActive(true);

            _playerFollowTarget = followTarget;
            _playerCamera?.SetFollowTarget(_playerFollowTarget);
            _deathCamera?.SetFollowTarget(_playerFollowTarget);

            SetCloseCameraActive();
            // Use the camera we just activated directly - Camera.main can return null in the
            // first frame after a scene transition because the tag-based lookup hasn't
            // observed the newly-activated GameObject yet.
            var activeCam = (_playerCamera as CustomCameraController)?.Camera;
            _themeManagerData.SetBackgroundColor(activeCam != null ? activeCam : Camera.main);

            var shipGO = _playerFollowTarget.gameObject;
            var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
            if (shipCustomizer != null)
                shipCustomizer.Configure(_playerCamera);

            // Snap camera to correct initial position to prevent retaining
            // stale end-game or transition state from the previous session.
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();

            ApplyCameraGraphicsSettings();
        }

        /// <summary>
        /// Configures the end camera to follow the given target with the vessel's
        /// camera settings applied. Used by Menu_Main to follow the autopilot vessel.
        /// </summary>
        public void SetupEndCameraFollow(Transform followTarget)
        {
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            endCamera.SetFollowTarget(followTarget);

            var customizer = followTarget.GetComponent<VesselCameraCustomizer>();
            customizer.Configure(endCamera);

            endCamera.SnapToTarget();
            SetEndCameraActive();
            ApplyCameraGraphicsSettings();
            _themeManagerData.SetBackgroundColor(Camera.main);
        }

        /// <summary>
        /// Point the end camera at an arbitrary follow target and make it live - the shared
        /// "replay camera" for modes that replay a moment (e.g. Astro League goal replays).
        /// Unlike <see cref="SetupEndCameraFollow"/> the target needs no VesselCameraCustomizer
        /// (a replay ghost is not a vessel); one is applied when present.
        /// </summary>
        public void SetupReplayCameraFollow(Transform followTarget, Vector3? followOffset = null)
        {
            if (endCamera == null || followTarget == null) return;
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            endCamera.SetFollowTarget(followTarget);
            if (followTarget.TryGetComponent(out VesselCameraCustomizer customizer))
                customizer.Configure(endCamera);
            // A non-vessel target (a replay ghost) has no customizer to size the framing, so the
            // caller supplies the follow offset explicitly - the end camera otherwise keeps
            // whatever offset its last vessel target left behind (far too close for a ball).
            if (followOffset.HasValue)
                endCamera.SetFollowOffset(followOffset.Value);

            endCamera.SnapToTarget();
            SetEndCameraActive();
            ApplyCameraGraphicsSettings();
        }

        /// <summary>
        /// Return from the replay/end camera to the gameplay follow camera (which still holds its
        /// vessel follow target), snapped so no stale replay framing bleeds into play.
        /// </summary>
        public void RestoreGameplayCamera()
        {
            if (_playerFollowTarget == null) return;
            SetCloseCameraActive();
            SnapPlayerCameraToTarget();
        }

        public void SetMainMenuCameraActive()
        {
            if (mainMenuCamera != null)
            {
                mainMenuCamera.Priority = ActivePriority;
                mainMenuCamera.gameObject.SetActive(true);
            }
            else
            {
                CSDebug.LogWarning("[CameraManager] Main menu camera is not assigned!");
            }

            if (_playerCamera is CustomCameraController pcc)
                pcc.Deactivate();
            if (_deathCamera is CustomCameraController dcc)
                dcc.Deactivate();
            if (endCamera != null)
                endCamera.Deactivate();

            _activeController = null;
            Invoke("LookAtCrystal", 1f);
        }

        void LookAtCrystal()
        {
            if (mainMenuCamera && cellData != null)
                mainMenuCamera.LookAt = cellData.CrystalTransform;
        }

        public void SetCloseCameraActive() => SetActiveCamera(_playerCamera);

        public void SetDeathCameraActive() => SetActiveCamera(_deathCamera);

        public void SetEndCameraActive() => SetActiveCamera(endCamera);

        void SetActiveCamera(ICameraController controller)
        {
                if (_playerCamera != null) _playerCamera.Deactivate();
                if (_deathCamera != null) _deathCamera.Deactivate();
                if (endCamera != null) endCamera.Deactivate();

            controller?.Activate();
            _activeController = controller;
            if (mainMenuCamera != null)
                mainMenuCamera.gameObject.SetActive(false);
        }

        public ICameraController GetActiveController() => _activeController;

        /// <summary>
        /// Deactivates all managed cameras (player, death, end) without activating a replacement.
        /// Used by the menu to hand control to the Cinemachine-driven main menu camera.
        /// </summary>
        public void DeactivateAllCameras()
        {
            if (_playerCamera != null) _playerCamera.Deactivate();
            if (_deathCamera != null) _deathCamera.Deactivate();
            if (endCamera != null) endCamera.Deactivate();
            _activeController = null;
        }

        /// <summary>
        /// Snaps the player camera to its follow target's current position.
        /// Call after vessel teleport or end-game cinematic to reset the camera.
        /// </summary>
        public void SnapPlayerCameraToTarget()
        {
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
        }

        public void SetNormalizedCloseCameraDistance(float normalizedDistance)
        {
            if (_playerCamera == null) return;
            // float close = CloseCamDistance > 0 ? CloseCamDistance : 10f;
            // float far = FarCamDistance > 0 ? FarCamDistance : 40f;
            // float distance = Mathf.Lerp(close, far, normalizedDistance);
            // _playerCamera.SetCameraDistance(distance);
        }
    }
}
