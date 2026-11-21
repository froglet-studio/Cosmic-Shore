// Ported verbatim from Assets/_Scripts/Controller/Managers/CameraManager.cs
// (CameraManager unit 2026-07-10), replacing the type-preserving shell
// (PORT Deviation #12, retired — the ShellCameraState mirror is gone; consumers
// observe the real state: GetActiveController / PlayerFollowTarget / the managed
// controllers' GameObject activity). Mechanical substitutions: UnityEngine →
// CosmicShore.Engine; Obvious.Soap → CosmicShore.Engine.Soap;
// UnityEngine.SceneManagement → CosmicShore.Engine (engine SceneManager).
// TWO carried deviation families, both marked inline:
//   1. camera arc (Unity.Cinemachine) — the `CinemachineCamera mainMenuCamera`
//      member and its Priority/LookAt/SetActive lines, the SAME commented family
//      MainMenuCameraController carries. Restore when the Cinemachine replacement
//      ports.
//   2. graphics-settings family (DisplayGraphicsSettings / GraphicsSettingsData /
//      GraphicsSettingsApplier, 394L across three upstream Settings files,
//      URP/QualitySettings-bound) — the FOV + post-process-AA sync pushed onto the
//      managed cameras. Restore when the Settings family ports (anticipated by the
//      shell's own drift note for upstream c833c580).
// Everything else is LIVE: the camera-trio discovery (transform.Find +
// AddComponent<CustomCameraController>), gameplay/end camera setup (follow targets,
// VesselCameraCustomizer.Configure, SnapToTarget), active-camera switching,
// DeactivateAllCameras, SnapPlayerCameraToTarget, scene-name routing, the SOAP
// subscription pair, and the theme background-color writes.
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Engine.Soap;
// PORT Deviation (camera arc 2026-07-10 — restore when Cinemachine ports): using Unity.Cinemachine;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;
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
        // PORT Deviation (camera arc 2026-07-10, CinemachineCamera — restore when Cinemachine ports):
        // [SerializeField] private CinemachineCamera mainMenuCamera;
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
            // is the spawn-proof place to apply — no per-camera scene reference needed.
            // PORT Deviation (graphics-settings family 2026-07-10 — restore when DisplayGraphicsSettings ports):
            // DisplayGraphicsSettings.OnFieldOfViewChanged += HandleCameraGraphicsChanged;
            // DisplayGraphicsSettings.OnAnySettingChanged += HandleCameraGraphicsChanged;
        }

        void OnDisable()
        {
            _onReturnToMainMenu.OnRaised -= OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised -= SetupGamePlayCameras;

            // PORT Deviation (graphics-settings family 2026-07-10 — restore when DisplayGraphicsSettings ports):
            // DisplayGraphicsSettings.OnFieldOfViewChanged -= HandleCameraGraphicsChanged;
            // DisplayGraphicsSettings.OnAnySettingChanged -= HandleCameraGraphicsChanged;
        }

        // PORT Deviation (graphics-settings family 2026-07-10 — restore when DisplayGraphicsSettings ports):
        // void HandleCameraGraphicsChanged(float _) => ApplyCameraGraphicsSettings();
        // void HandleCameraGraphicsChanged(GraphicsSettingsData _) => ApplyCameraGraphicsSettings();

        /// <summary>
        /// Pushes the saved Field-of-View and post-process AA (FXAA/SMAA/TAA) onto every managed
        /// camera. MSAA is global on the URP asset (handled by DisplayGraphicsSettings),
        /// so this only sets FOV + the per-camera post-AA mode. Null-safe and called on every camera
        /// setup, so a vessel that spawns later still gets the right look.
        /// PORT Deviation (graphics-settings family 2026-07-10): body carried commented — the
        /// method stays so the setup call sites keep the upstream shape; no-op until the
        /// DisplayGraphicsSettings family ports.
        /// </summary>
        public void ApplyCameraGraphicsSettings()
        {
            // var s = DisplayGraphicsSettings.Instance;
            // if (s == null) return;
            // var d = s.Current;
            // ApplyToCamera((_playerCamera as CustomCameraController)?.Camera, d);
            // ApplyToCamera((_deathCamera as CustomCameraController)?.Camera, d);
            // ApplyToCamera(endCamera != null ? endCamera.Camera : null, d);
        }

        // PORT Deviation (graphics-settings family 2026-07-10 — restore when DisplayGraphicsSettings ports):
        // static void ApplyToCamera(Camera cam, GraphicsSettingsData d)
        // {
        //     if (cam == null) return;
        //     if (!cam.orthographic) cam.fieldOfView = d.FieldOfView;
        //     GraphicsSettingsApplier.ApplyCameraAntiAliasing(cam, d.AntiAliasing);
        // }

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
            // Use the camera we just activated directly — Camera.main can return null in the
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

        public void SetMainMenuCameraActive()
        {
            // PORT Deviation (camera arc 2026-07-10, CinemachineCamera — restore when Cinemachine ports):
            // if (mainMenuCamera != null)
            // {
            //     mainMenuCamera.Priority = ActivePriority;
            //     mainMenuCamera.gameObject.SetActive(true);
            // }
            // else
            // {
            //     CSDebug.LogWarning("[CameraManager] Main menu camera is not assigned!");
            // }

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
            // PORT Deviation (camera arc 2026-07-10, CinemachineCamera.LookAt — restore when Cinemachine ports):
            // if (mainMenuCamera && cellData != null)
            //     mainMenuCamera.LookAt = cellData.CrystalTransform;
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
            // PORT Deviation (camera arc 2026-07-10, CinemachineCamera — restore when Cinemachine ports):
            // if (mainMenuCamera != null)
            //     mainMenuCamera.gameObject.SetActive(false);
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
