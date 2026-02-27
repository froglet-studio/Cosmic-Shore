using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
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

        [Inject]
        SceneNameListSO _sceneNameList;

        [Inject]
        GameDataSO _gameData;
    
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

        private Transform _playerFollowTarget;
        private const int ActivePriority = 10;
        private CancellationTokenSource _lookAtCrystalCts;

        public Transform PlayerFollowTarget
        {
            get => _playerFollowTarget;
            set => _playerFollowTarget = value;
        }

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
        }

        void OnDisable()
        {
            _onReturnToMainMenu.OnRaised -= OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised -= SetupGamePlayCameras;
            _lookAtCrystalCts?.Cancel();
            _lookAtCrystalCts?.Dispose();
            _lookAtCrystalCts = null;
        }

        void Start()
        {
            InitializeSceneCamera();
        }

        private void InitializeSceneCamera()
        {
            if (_sceneNameList == null)
            {
                Debug.LogError("[CameraManager] _sceneNameList was not injected — check AppManager DI registration.");
                return;
            }

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
            _gameData.InvokeSceneTransition(true);
        }

        public void SetupGamePlayCameras(Transform followTarget)
        {
            if(!gameObject.activeInHierarchy) gameObject.SetActive(true);

            _playerFollowTarget = followTarget;
            _playerCamera?.SetFollowTarget(_playerFollowTarget);
            _deathCamera?.SetFollowTarget(_playerFollowTarget);
            _themeManagerData.SetBackgroundColor(Camera.main);

            SetCloseCameraActive();

            var shipGO = _playerFollowTarget.gameObject;
            var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
            shipCustomizer.Configure(_playerCamera);

            // Snap camera to correct initial position to prevent retaining
            // stale end-game or transition state from the previous session.
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
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
            ScheduleLookAtCrystal();
        }

        void ScheduleLookAtCrystal()
        {
            _lookAtCrystalCts?.Cancel();
            _lookAtCrystalCts?.Dispose();
            _lookAtCrystalCts = new CancellationTokenSource();
            LookAtCrystalDelayed(_lookAtCrystalCts.Token).Forget();
        }

        async UniTaskVoid LookAtCrystalDelayed(CancellationToken ct)
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            if (mainMenuCamera)
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
            mainMenuCamera.gameObject.SetActive(false);
        }

        public ICameraController GetActiveController() => _activeController;

        /// <summary>
        /// Snaps the player camera to its follow target's current position.
        /// Call after vessel teleport or end-game cinematic to reset the camera.
        /// </summary>
        public void SnapPlayerCameraToTarget()
        {
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
        }

        /// <summary>
        /// Switches to the Cinemachine main menu camera and sets it to follow
        /// the given target (typically the autopilot vessel in Menu_Main).
        /// Cancels the default crystal look-at so the camera tracks the vessel.
        /// </summary>
        public void FollowVesselInMainMenu(Transform target)
        {
            if (!mainMenuCamera) return;

            SetMainMenuCameraActive();

            // Cancel the crystal look-at scheduled by SetMainMenuCameraActive
            _lookAtCrystalCts?.Cancel();
            _lookAtCrystalCts?.Dispose();
            _lookAtCrystalCts = null;

            mainMenuCamera.Follow = target;
            mainMenuCamera.LookAt = target;
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
