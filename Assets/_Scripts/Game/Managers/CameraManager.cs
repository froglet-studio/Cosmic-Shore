using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utilities;
using CosmicShore.Utility;
using System.Collections;
using CosmicShore.Game;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraManager : SingletonPersistent<CameraManager>, ICameraManager
{
    [SerializeField]
    ThemeManagerDataContainerSO _themeManagerData;

    [SerializeField] CinemachineCamera mainMenuCamera;
    [SerializeField] CustomCameraController playerCamera;
    [SerializeField] CustomCameraController deathCamera;
    [SerializeField] CustomCameraController endCamera;

    private ICameraController activeController;

    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    // Drift stuff
    [HideInInspector]
    public bool FollowOverride = false;
    [HideInInspector]
    public bool FixedFollow = false;
    [HideInInspector]
    public bool isOrthographic = false;

    [HideInInspector]
    public float CloseCamDistance;
    [HideInInspector]
    public float FarCamDistance;

    [SerializeField]
    float startTransitionDistance = 40f;

    Camera vCam;

    ICameraOffsetHandler offsetHandler;
    
    public bool FreezeRuntimeOffset { get; set; } = false;

    public override void Awake()
    {
        base.Awake();

        EnsureController(ref playerCamera, "CM PlayerCam");
        EnsureController(ref deathCamera, "CM DeathCam");
        EnsureController(ref endCamera, "CM EndCam");
    }

    void EnsureController(ref CustomCameraController controller, string name)
    {
        if (controller == null)
        {
            Transform t = transform.Find(name);
            if (t)
                controller = t.gameObject.GetComponent<CustomCameraController>() ?? t.gameObject.AddComponent<CustomCameraController>();
        }
        else if (controller.GetComponent<CustomCameraController>() == null)
        {
            controller = controller.gameObject.AddComponent<CustomCameraController>();
        }
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

        // Restore original offset when disabled
        offsetHandler?.Restore();
    }

    void Start()
    {
        vCam = playerCamera.Camera;
        offsetHandler = new CameraOffsetHandler(playerCamera, this);
        offsetHandler.Initialize();
        OnMainMenu();
    }

    void LateUpdate()
    {
        if (Application.isPlaying && offsetHandler != null)
        {
            offsetHandler.Apply();
        }
    }

    public Transform GetCloseCamera()
    {
        return playerCamera.transform;
    }

    public Vector3 CurrentOffset => offsetHandler?.CurrentOffset ?? Vector3.zero;

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
        playerCamera.SetFollowTarget(playerFollowTarget);
        deathCamera.SetFollowTarget(playerFollowTarget);
        _themeManagerData.SetBackgroundColor(Camera.main);
        
        SetCloseCameraActive();
        
        var shipGO = playerFollowTarget.gameObject;
        var shipCustomizer = shipGO.GetComponent<ShipCameraCustomizer>();
        if (shipCustomizer != null)
        {
            shipCustomizer.Initialize(shipGO.GetComponent<IShip>());
        }
    }

    public void SetMainMenuCameraActive()
    {
        SetActiveCamera(mainMenuCamera);
        Invoke("LookAtCrystal", 1); // Delay to allow the old crystal to be destroyed
    }

    void LookAtCrystal()
    {
        mainMenuCamera.LookAt = NodeControlManager.Instance.GetNearestNode(Vector3.zero).GetCrystal().transform;
    }

    public void SetCloseCameraActive()
    {
        SetActiveCamera(playerCamera);
    }

    public void SetDeathCameraActive()
    {
        SetActiveCamera(deathCamera);
    }

    public void SetEndCameraActive()
    {
        SetActiveCamera(endCamera);
    }

    public void SetFixedFollowOffset(Vector3 offset)
    {
        FixedFollow = true;
        StartCoroutine(SetFollowOffsetCoroutine(offset));
    }

    IEnumerator SetFollowOffsetCoroutine(Vector3 offset)
    {
        yield return new WaitForSeconds(1);
        offsetHandler.Initialize();
        offsetHandler.SetOffset(offset);
    }

    void SetActiveCamera(Component activeCamera)
    {
        Orthographic(isOrthographic);

        // disable all first
        playerCamera.Deactivate();
        deathCamera.Deactivate();
        endCamera.Deactivate();
        mainMenuCamera.Priority = inactivePriority;

        // enable & assign activeController
        if (activeCamera == mainMenuCamera)
        {
            mainMenuCamera.Priority = activePriority;
      
        }
        else if (activeCamera == playerCamera)
        {
            playerCamera.Activate();
            activeController = playerCamera;
            if (playerCamera.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
                camData.renderPostProcessing = true;
            offsetHandler.Initialize();
            Vector3 finalOffset = offsetHandler.CurrentOffset;
            offsetHandler.SetOffset(finalOffset + Vector3.back * startTransitionDistance);
            SetOffsetPosition(finalOffset);
        }
        else if (activeCamera == deathCamera)
        {
            deathCamera.gameObject.SetActive(true);
            activeController = deathCamera;
        }
        else if (activeCamera == endCamera)
        {
            endCamera.gameObject.SetActive(true);
            activeController = endCamera;
        }
    }

    public ICameraController GetActiveController()
    {
        return activeController;
    }

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        offsetHandler.FixedFollow = FixedFollow;
        offsetHandler.SetNormalizedDistance(this, normalizedDistance, CloseCamDistance, FarCamDistance);
    }

    public void SetOffsetPosition(Vector3 position)
    {
        offsetHandler.SetOffsetPosition(this, position);
    }

    void Orthographic(bool isOrthographic)
    {
        PostProcessingManager.Instance.Orthographic(isOrthographic);
        playerCamera.SetOrthographic(isOrthographic, 1300);

    }

    public void ZoomCloseCameraOut(float growthRate)
    {
        offsetHandler.FixedFollow = FixedFollow;
        offsetHandler.ZoomOut(this, growthRate, FarCamDistance);
    }

    public void ResetCloseCameraToNeutral(float shrinkRate)
    {
        offsetHandler.FixedFollow = FixedFollow;
        offsetHandler.ResetToNeutral(this, shrinkRate, CloseCamDistance);
    }
}

internal class CameraOffsetHandler : ICameraOffsetHandler
{
    private readonly CustomCameraController playerCamera;
    private readonly MonoBehaviour owner;
    private readonly Camera vCam;

    private Vector3 originalFollowOffset;
    private Vector3 runtimeFollowOffset;
    private bool hasOriginalOffset;

    private bool zoomingOut;
    private Coroutine zoomOutCoroutine;
    private Coroutine returnToNeutralCoroutine;
    private Coroutine lerper;

    public bool FixedFollow { get; set; }

    public Vector3 CurrentOffset => runtimeFollowOffset;

    public CameraOffsetHandler(CustomCameraController cam, MonoBehaviour owner)
    {
        playerCamera = cam;
        this.owner = owner;
        vCam = cam.Camera;
    }

    public void Initialize()
    {
        if (!hasOriginalOffset)
        {
            originalFollowOffset = playerCamera.GetFollowOffset();
            runtimeFollowOffset = originalFollowOffset;
            hasOriginalOffset = true;
        }
    }

    public void Apply()
    {
        playerCamera.SetFollowOffset(runtimeFollowOffset);
    }

    public void Restore()
    {
        if (hasOriginalOffset)
            playerCamera.SetFollowOffset(originalFollowOffset);
    }

    public void SetOffset(Vector3 offset)
    {
        runtimeFollowOffset = offset;
        playerCamera.SetFollowOffset(offset);
    }

    public void SetNormalizedDistance(MonoBehaviour caller, float normalizedDistance, float closeCamDistance, float farCamDistance)
    {
        if (FixedFollow) return;
        Initialize();

        Vector3 targetOffset = new Vector3(0, 0, normalizedDistance);
        if (runtimeFollowOffset != targetOffset)
        {
            ClipPlaneAndOffsetLerper(normalizedDistance, closeCamDistance, farCamDistance);
        }
    }

    public void SetOffsetPosition(MonoBehaviour caller, Vector3 position)
    {
        if (FixedFollow) return;
        Initialize();

        if (runtimeFollowOffset != position)
        {
            ClipPlaneAndOffsetLerper(position);
        }
    }

    public void ZoomOut(MonoBehaviour caller, float growthRate, float farCamDistance)
    {
        if (FixedFollow) return;
        if (returnToNeutralCoroutine != null)
        {
            caller.StopCoroutine(returnToNeutralCoroutine);
            returnToNeutralCoroutine = null;
        }
        zoomingOut = true;
        zoomOutCoroutine = caller.StartCoroutine(ZoomOutCloseCameraCoroutine(growthRate, farCamDistance));
    }

    public void ResetToNeutral(MonoBehaviour caller, float shrinkRate, float closeCamDistance)
    {
        if (FixedFollow) return;
        if (zoomOutCoroutine != null)
        {
            caller.StopCoroutine(zoomOutCoroutine);
            zoomOutCoroutine = null;
        }
        zoomingOut = false;
        returnToNeutralCoroutine = caller.StartCoroutine(ReturnCloseCameraToNeutralCoroutine(shrinkRate, closeCamDistance));
    }

    private void ClipPlaneAndOffsetLerper(float normalizedDistance, float closeCamDistance, float farCamDistance)
    {
        float closeCamClipPlane = .5f;
        float farCamClipPlane = .7f;
        if (lerper != null)
            owner.StopCoroutine(lerper);

        float startNormalized = (runtimeFollowOffset.z - closeCamDistance) / (farCamDistance - closeCamDistance);

        lerper = owner.StartCoroutine(LerpUtilities.LerpingCoroutine(startNormalized,
            normalizedDistance, 1.5f, (i) =>
            {
                vCam.nearClipPlane = (farCamClipPlane - closeCamClipPlane) * i + closeCamClipPlane;
                SetOffset(new Vector3(0, 0, (farCamDistance - closeCamDistance) * i + closeCamDistance));
            }));
    }

    private void ClipPlaneAndOffsetLerper(Vector3 offsetPosition)
    {
        if (lerper != null)
            owner.StopCoroutine(lerper);

        lerper = owner.StartCoroutine(LerpUtilities.LerpingCoroutine(runtimeFollowOffset,
            offsetPosition, 1.5f, (i) =>
            {
                SetOffset(i);
            }));
    }

    private IEnumerator ZoomOutCloseCameraCoroutine(float growthRate, float farCamDistance)
    {
        Initialize();

        while (zoomingOut && runtimeFollowOffset.z > farCamDistance)
        {
            SetOffset(runtimeFollowOffset + Time.deltaTime * Mathf.Abs(growthRate) * -Vector3.forward);
            yield return null;
        }
    }

    private IEnumerator ReturnCloseCameraToNeutralCoroutine(float shrinkRate, float closeCamDistance)
    {
        Initialize();

        while (runtimeFollowOffset.z <= closeCamDistance)
        {
            SetOffset(runtimeFollowOffset + Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward);
            yield return null;
        }

        SetOffset(new Vector3(0, 0, closeCamDistance));
    }
}
