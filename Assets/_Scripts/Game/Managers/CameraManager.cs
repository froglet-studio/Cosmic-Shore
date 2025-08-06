using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utilities;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;


public class CameraManager : Singleton<CameraManager>
{
    [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
    [SerializeField] private ScriptableEventNoParam _onReturnToMainMenu;
    [SerializeField] private ScriptableEventNoParam _onPlayGame;
    [SerializeField] private ScriptableEventNoParam _onGameOver;

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
    private const bool FollowOverride = false;

    private Camera _vCam;
    private IShipStatus _shipStatus;

    public override void Awake()
    {
        base.Awake();
        _playerCamera = GetOrFindCameraController("CM PlayerCam");
        _deathCamera = GetOrFindCameraController("CM DeathCam");
        endCamera = endCamera ?? GetOrFindCameraController("CM EndCam") as CustomCameraController;
    }
    
    private void OnEnable()
    {
        _onReturnToMainMenu.OnRaised += OnEnteredMainMenu;
        _onPlayGame.OnRaised += SetupGamePlayCameras;
        _onGameOver.OnRaised += SetEndCameraActive;
    }

    void OnDisable()
    {
        _onReturnToMainMenu.OnRaised -= OnEnteredMainMenu;
        _onPlayGame.OnRaised -= SetupGamePlayCameras;
        _onGameOver.OnRaised -= SetEndCameraActive;
    }

    void Start()
    {
        _vCam = (_playerCamera as CustomCameraController)?.Camera;
        OnEnteredMainMenu();
    }

    // TODO - Remove this later, not needed
    public void Initialize(IShipStatus shipStatus) =>  this._shipStatus = shipStatus;
    
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
        Debug.LogWarning($"[CameraManager] Could not find camera controller: {name}");
        return null;
    }

    public Transform GetCloseCamera() => (_playerCamera as CustomCameraController)?.transform;

    public Vector3 CurrentOffset => (_playerCamera as CustomCameraController)?.GetFollowOffset() ?? Vector3.zero;
    
    void OnEnteredMainMenu()
    {
        SetMainMenuCameraActive();
        _themeManagerData.SetBackgroundColor(Camera.main);
    }

    public void SetupGamePlayCameras()
    {
        _playerFollowTarget = FollowOverride ? _shipStatus.ShipCameraCustomizer.FollowTarget : _shipStatus.Transform;
        SetupGamePlayCameras(_playerFollowTarget);
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        _playerFollowTarget = _transform;
        _playerCamera?.SetFollowTarget(_playerFollowTarget);
        _deathCamera?.SetFollowTarget(_playerFollowTarget);
        _themeManagerData.SetBackgroundColor(Camera.main);

        SetCloseCameraActive();

        var shipGO = _playerFollowTarget.gameObject;
        var shipCustomizer = shipGO.GetComponent<ShipCameraCustomizer>();
        if (shipCustomizer != null)
            shipCustomizer.Initialize(shipGO.GetComponent<IShip>());
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
            Debug.LogWarning("[CameraManager] Main menu camera is not assigned!");
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
        if (mainMenuCamera)
            mainMenuCamera.LookAt = CellControlManager.Instance.GetNearestCell(Vector3.zero).GetCrystal().transform;
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
    }

    public ICameraController GetActiveController() => _activeController;

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        if (_playerCamera == null) return;
        // float close = CloseCamDistance > 0 ? CloseCamDistance : 10f;
        // float far = FarCamDistance > 0 ? FarCamDistance : 40f;
        // float distance = Mathf.Lerp(close, far, normalizedDistance);
        // _playerCamera.SetCameraDistance(distance);
    }
}
