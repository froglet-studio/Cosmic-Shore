using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utilities;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;
using Unity.Cinemachine;
using UnityEngine;


public class CameraManager : Singleton<CameraManager>
{
    [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
    
    [SerializeField] 
    ScriptableEventNoParam _onReturnToMainMenu;
    
    [SerializeField] 
    ScriptableEventNoParam _onPlayGame;
        
    [SerializeField]
    ScriptableEventNoParam _onGameOver;

    private ICameraController playerCamera;
    private ICameraController deathCamera;
    [SerializeField] CustomCameraController endCamera; // this can be as is if only one

    private ICameraController activeController;

    [SerializeField] private CinemachineCamera mainMenuCamera;
    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    [HideInInspector] public bool FollowOverride = false;
    [HideInInspector] public bool FixedFollow = false;

    [HideInInspector] public float CloseCamDistance;
    [HideInInspector] public float FarCamDistance;

    [SerializeField] float startTransitionDistance = 40f;

    private Camera vCam;
    private IShipStatus shipStatus;

    public bool FreezeRuntimeOffset { get; set; } = false;

    public override void Awake()
    {
        base.Awake();

        // Find or assign ICameraController references
        playerCamera = GetOrFindCameraController("CM PlayerCam");
        deathCamera = GetOrFindCameraController("CM DeathCam");
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

        // Restore original offset when disabled
        // RestoreOriginalOffset();
    }

    void Start()
    {
        vCam = (playerCamera as CustomCameraController)?.Camera;
        OnEnteredMainMenu();
    }

    // TODO - Remove this later, not needed
    public void Initialize(IShipStatus shipStatus) =>  this.shipStatus = shipStatus;
    
    // void EnsureController(ref CustomCameraController controller, string name)
    
    private ICameraController GetOrFindCameraController(string name)
    {
        Transform t = transform.Find(name);
        if (t)
        {
            // use GetOrAdd extension
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

    public Transform GetCloseCamera() => (playerCamera as CustomCameraController)?.transform;

    public Vector3 CurrentOffset => (playerCamera as CustomCameraController)?.GetFollowOffset() ?? Vector3.zero;
    
    void OnEnteredMainMenu()
    {
        SetMainMenuCameraActive();
        _themeManagerData.SetBackgroundColor(Camera.main);
    }

    public void SetupGamePlayCameras()
    {
        playerFollowTarget = FollowOverride ? shipStatus.ShipCameraCustomizer.FollowTarget : shipStatus.Transform;
        SetupGamePlayCameras(playerFollowTarget);
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        playerFollowTarget = _transform;
        playerCamera?.SetFollowTarget(playerFollowTarget);
        deathCamera?.SetFollowTarget(playerFollowTarget);
        _themeManagerData.SetBackgroundColor(Camera.main);

        SetCloseCameraActive();

        var shipGO = playerFollowTarget.gameObject;
        var shipCustomizer = shipGO.GetComponent<ShipCameraCustomizer>();
        if (shipCustomizer != null)
            shipCustomizer.Initialize(shipGO.GetComponent<IShip>());
    }

    public void SetMainMenuCameraActive()
    {
        // 1. Set Cinemachine main menu camera to active/priority
        if (mainMenuCamera != null)
        {
            mainMenuCamera.Priority = activePriority;
            mainMenuCamera.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[CameraManager] Main menu camera is not assigned!");
        }

        // 2. Deactivate all gameplay cameras
        if (playerCamera is CustomCameraController pcc)
            pcc.Deactivate();
        if (deathCamera is CustomCameraController dcc)
            dcc.Deactivate();
        if (endCamera != null)
            endCamera.Deactivate();

        activeController = null;

        // 3. If you had LookAt logic for a crystal, restore it here:
        Invoke("LookAtCrystal", 1f);
    }

    void LookAtCrystal()
    {
        if (mainMenuCamera)
            mainMenuCamera.LookAt = CellControlManager.Instance.GetNearestCell(Vector3.zero).GetCrystal().transform;
    }


    public void SetCloseCameraActive() => SetActiveCamera(playerCamera);

    public void SetDeathCameraActive() => SetActiveCamera(deathCamera);

    public void SetEndCameraActive() => SetActiveCamera(endCamera);

    void SetActiveCamera(ICameraController controller)
    {
        if (playerCamera != null) playerCamera.Deactivate();
        if (deathCamera != null) deathCamera.Deactivate();
        if (endCamera != null) endCamera.Deactivate();

        controller?.Activate();
        activeController = controller;
    }

    public ICameraController GetActiveController() => activeController;

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        if (playerCamera == null) return;
        float close = CloseCamDistance > 0 ? CloseCamDistance : 10f;
        float far = FarCamDistance > 0 ? FarCamDistance : 40f;
        float distance = Mathf.Lerp(close, far, normalizedDistance);
        playerCamera.SetCameraDistance(distance);
    }

    public void ZoomCloseCameraOut(float rateNormalized)
    {
        if (playerCamera is CustomCameraController customCameraController)
            customCameraController.StartZoomOut(rateNormalized);
    }

    public void ResetCloseCameraToNeutral(float rateNormalized)
    {
        if (playerCamera is CustomCameraController customCameraController)
            customCameraController.StartZoomIn(rateNormalized * 5f);
    }

    // Overloads for ElementalFloat
    public void ZoomCloseCameraOut(ElementalFloat rate)
        => ZoomCloseCameraOut(rate.Value);

    public void ResetCloseCameraToNeutral(ElementalFloat rate)
        => ResetCloseCameraToNeutral(rate.Value);
}
