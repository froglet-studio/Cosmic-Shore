using Unity.Cinemachine;
using CosmicShore.Core;
using CosmicShore.Utility;
using System.Collections;
using UnityEngine;
using CosmicShore;
using CosmicShore.Utilities;
using CosmicShore.Game.CameraSystem;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField]
    ThemeManagerDataContainerSO _themeManagerData;

    [SerializeField] CinemachineCamera mainMenuCamera;
    [SerializeField] CustomCameraController playerCamera;
    [SerializeField] CustomCameraController deathCamera;
    [SerializeField] CustomCameraController endCamera;

    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    // Runtime-only properties (not serialized)
    private Vector3 originalFollowOffset;
    private Vector3 runtimeFollowOffset;
    private bool hasOriginalOffset = false;

    // Drift stuff
    bool zoomingOut;

    public bool FollowOverride = false;
    public bool FixedFollow = false;
    public bool isOrthographic = false;

    public float CloseCamDistance;
    public float FarCamDistance;

    Camera vCam;

    Coroutine zoomOutCoroutine;
    Coroutine returnToNeutralCoroutine;
    Coroutine lerper;

    void Awake()
    {
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
        RestoreOriginalOffset();
    }

    void Start()
    {
        vCam = playerCamera.Camera;
        InitializeRuntimeOffset();
        OnMainMenu();
    }

    void LateUpdate()
    {
        if (Application.isPlaying && hasOriginalOffset)
        {
            ApplyRuntimeOffset();
        }
    }

    private void ApplyRuntimeOffset()
    {
        playerCamera.SetFollowOffset(runtimeFollowOffset);
    }

    private void RestoreOriginalOffset()
    {
        if (hasOriginalOffset)
        {
            playerCamera.SetFollowOffset(originalFollowOffset);
        }
    }

    private void SetRuntimeFollowOffset(Vector3 offset)
    {
        runtimeFollowOffset = offset;
        playerCamera.SetFollowOffset(runtimeFollowOffset);
    }

    private void InitializeRuntimeOffset()
    {
        if (!hasOriginalOffset)
        {
            originalFollowOffset = playerCamera.GetFollowOffset();
            runtimeFollowOffset = originalFollowOffset;
            hasOriginalOffset = true;
        }
    }

    public Transform GetCloseCamera()
    {
        return playerCamera.transform;
    }

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
        yield return new WaitForSeconds(1); // Allow time for camera to stabilize
        InitializeRuntimeOffset();
        SetRuntimeFollowOffset(offset);
    }

    void SetActiveCamera(Component activeCamera)
    {
        Orthographic(isOrthographic);
        Debug.Log($"SetActiveCamera {activeCamera.name}");

        mainMenuCamera.Priority = inactivePriority;
        playerCamera.gameObject.SetActive(false);
        deathCamera.gameObject.SetActive(false);
        endCamera.gameObject.SetActive(false);

        if (activeCamera == mainMenuCamera)
        {
            mainMenuCamera.Priority = activePriority;
        }
        else if (activeCamera == playerCamera)
        {
            playerCamera.gameObject.SetActive(true);
            InitializeRuntimeOffset();
            SetOffsetPosition(runtimeFollowOffset);
        }
        else if (activeCamera == deathCamera)
        {
            deathCamera.gameObject.SetActive(true);
        }
        else if (activeCamera == endCamera)
        {
            endCamera.gameObject.SetActive(true);
        }
    }

    void ClipPlaneAndOffsetLerper(float normalizedDistance)
    {
        float CloseCamClipPlane = .5f;
        float FarCamClipPlane = .7f;
        if (lerper != null)
            StopCoroutine(lerper);

        float startNormalized = (runtimeFollowOffset.z - CloseCamDistance) / (FarCamDistance - CloseCamDistance);

        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine(startNormalized,
            normalizedDistance, 1.5f, (i) =>
            {
                vCam.nearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                SetRuntimeFollowOffset(new Vector3(0, 0, (FarCamDistance - CloseCamDistance) * i + CloseCamDistance));
            }));
    }

    void ClipPlaneAndOffsetLerper(Vector3 offsetPosition)
    {
        float CloseCamClipPlane = .5f;
        //float FarCamClipPlane = .7f;
        if (lerper != null)
            StopCoroutine(lerper);

        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine(runtimeFollowOffset,
            offsetPosition, 1.5f, (i) =>
            {
                SetRuntimeFollowOffset(i);
            }));
    }

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        if (FixedFollow) return;
        InitializeRuntimeOffset();

        Vector3 targetOffset = new Vector3(0, 0, normalizedDistance);
        if (runtimeFollowOffset != targetOffset)
        {
            ClipPlaneAndOffsetLerper(normalizedDistance);
        }
    }

    public void SetOffsetPosition(Vector3 position)
    {
        InitializeRuntimeOffset();

        if (runtimeFollowOffset != position)
        {
            ClipPlaneAndOffsetLerper(position);
        }
    }

    void Orthographic(bool isOrthographic)
    {
        PostProcessingManager.Instance.Orthographic(isOrthographic);
        playerCamera.SetOrthographic(isOrthographic, 1300);
    }

    public void ZoomCloseCameraOut(float growthRate)
    {
        if (FixedFollow) return;
        if (returnToNeutralCoroutine != null)
        {
            StopCoroutine(returnToNeutralCoroutine);
            returnToNeutralCoroutine = null;
        }
        zoomingOut = true;
        zoomOutCoroutine = StartCoroutine(ZoomOutCloseCameraCoroutine(growthRate));
    }

    IEnumerator ZoomOutCloseCameraCoroutine(float growthRate)
    {
        InitializeRuntimeOffset();

        while (zoomingOut && runtimeFollowOffset.z > FarCamDistance)
        {
            SetRuntimeFollowOffset(runtimeFollowOffset + Time.deltaTime * Mathf.Abs(growthRate) * -Vector3.forward);
            yield return null;
        }
    }

    public void ResetCloseCameraToNeutral(float shrinkRate)
    {
        if (FixedFollow) return;
        if (zoomOutCoroutine != null)
        {
            StopCoroutine(zoomOutCoroutine);
            zoomOutCoroutine = null;
        }
        zoomingOut = false;
        returnToNeutralCoroutine = StartCoroutine(ReturnCloseCameraToNeutralCoroutine(shrinkRate));
    }

    IEnumerator ReturnCloseCameraToNeutralCoroutine(float shrinkRate)
    {
        InitializeRuntimeOffset();

        while (runtimeFollowOffset.z <= CloseCamDistance)
        {
            SetRuntimeFollowOffset(runtimeFollowOffset + Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward);
            yield return null;
        }

        SetRuntimeFollowOffset(new Vector3(0, 0, CloseCamDistance));
    }}