using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utilities;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

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
    [HideInInspector] public bool isOrthographic = false;

    [HideInInspector] public float CloseCamDistance;
    [HideInInspector] public float FarCamDistance;

    [SerializeField] float startTransitionDistance = 40f;

    Camera vCam;

    public bool FreezeRuntimeOffset { get; set; } = false;

    public override void Awake()
    {
        base.Awake();

        // Find or assign ICameraController references
        playerCamera = GetOrFindCameraController("CM PlayerCam");
        deathCamera = GetOrFindCameraController("CM DeathCam");
        endCamera = endCamera ?? GetOrFindCameraController("CM EndCam") as CustomCameraController;
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
        Debug.LogWarning($"[CameraManager] Could not find camera controller: {name}");
        return null;
    }

    private void OnEnable()
    {
        GameManager.OnPlayGame += SetupGamePlayCameras;
        GameManager.OnGameOver += SetEndCameraActive;
    }

    void OnDisable()
    {
        GameManager.OnPlayGame -= SetupGamePlayCameras;
        GameManager.OnGameOver -= SetEndCameraActive;
    }

    void Start()
    {
        vCam = (playerCamera as CustomCameraController)?.Camera;
        OnMainMenu();
    }

    public Transform GetCloseCamera() => (playerCamera as CustomCameraController)?.transform;

    public Vector3 CurrentOffset => (playerCamera as CustomCameraController)?.GetFollowOffset() ?? Vector3.zero;

    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
        _themeManagerData.SetBackgroundColor(Camera.main);
    }

    public void SetupGamePlayCameras()
    {
        playerFollowTarget = FollowOverride ? Hangar.Instance.SelectedShip.ShipStatus.ShipCameraCustomizer.FollowTarget : Hangar.Instance.SelectedShip.Transform;
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
        if (mainMenuCamera != null)
            mainMenuCamera.LookAt = NodeControlManager.Instance.GetNearestNode(Vector3.zero).GetCrystal().transform;
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

    public void ZoomCloseCameraOut(float growthRate)
    {
        if (playerCamera == null) return;
        float start = playerCamera.GetCameraDistance();
        playerCamera.LerpCameraDistance(start, FarCamDistance, growthRate);
    }

    public void ResetCloseCameraToNeutral(float shrinkRate)
    {
        if (playerCamera == null) return;
        float start = playerCamera.GetCameraDistance();
        playerCamera.LerpCameraDistance(start, CloseCamDistance, shrinkRate);
    }
}
