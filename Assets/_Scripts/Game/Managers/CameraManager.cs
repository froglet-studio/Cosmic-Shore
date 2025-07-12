using Unity.Cinemachine;
using CosmicShore.Core;
using CosmicShore.Utility;
using System.Collections;
using UnityEngine;
using CosmicShore;
using CosmicShore.Utilities;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField]
    ThemeManagerDataContainerSO _themeManagerData;

    [SerializeField] CinemachineCamera mainMenuCamera;
    [SerializeField] CinemachineVirtualCameraBase playerCamera;
    [SerializeField] CinemachineVirtualCameraBase deathCamera;
    [SerializeField] CinemachineVirtualCameraBase endCamera;

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

    CinemachineCamera vCam;
    CinemachineFollow transposer;

    Coroutine zoomOutCoroutine;
    Coroutine returnToNeutralCoroutine;
    Coroutine lerper;

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
        vCam = playerCamera.gameObject.GetComponent<CinemachineCamera>();
        OnMainMenu();
    }

    void LateUpdate()
    {
        // Apply runtime offset without modifying the serialized property
        if (transposer != null && Application.isPlaying && hasOriginalOffset)
        {
            ApplyRuntimeOffset();
        }
    }

    private void ApplyRuntimeOffset()
    {
        // Use reflection to set the offset without marking scene dirty
        var field = typeof(CinemachineFollow).GetField("m_FollowOffset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(transposer, runtimeFollowOffset);
        }
    }

    private void RestoreOriginalOffset()
    {
        if (transposer != null && hasOriginalOffset)
        {
            var field = typeof(CinemachineFollow).GetField("m_FollowOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(transposer, originalFollowOffset);
            }
        }
    }

    private void SetRuntimeFollowOffset(Vector3 offset)
    {
        runtimeFollowOffset = offset;
    }

    private void InitializeRuntimeOffset()
    {
        if (transposer != null && !hasOriginalOffset)
        {
            originalFollowOffset = transposer.FollowOffset;
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
        playerCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        playerCamera.Follow = deathCamera.Follow = playerFollowTarget;
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
        mainMenuCamera.LookAt = CellControlManager.Instance.GetNearestCell(Vector3.zero).GetCrystal().transform;
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
        transposer = vCam.GetComponent<CinemachineFollow>();
        InitializeRuntimeOffset();
        SetRuntimeFollowOffset(offset);
    }

    void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        Orthographic(isOrthographic);
        Debug.Log($"SetActiveCamera {activeCamera.Name}");

        mainMenuCamera.Priority = inactivePriority;
        playerCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
        deathCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
        transposer = vCam.GetComponent<CinemachineFollow>();
        InitializeRuntimeOffset();

        if (activeCamera == playerCamera)
        {
            SetOffsetPosition(runtimeFollowOffset);
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
                vCam.Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
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
                //vCam.Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                SetRuntimeFollowOffset(i);
            }));
    }

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        if (FixedFollow) return;
        transposer = vCam.GetComponent<CinemachineFollow>();
        InitializeRuntimeOffset();

        Vector3 targetOffset = new Vector3(0, 0, normalizedDistance);
        if (runtimeFollowOffset != targetOffset)
        {
            ClipPlaneAndOffsetLerper(normalizedDistance);
        }
    }

    public void SetOffsetPosition(Vector3 position)
    {
        //offsetVector = position;
        transposer = vCam.GetComponent<CinemachineFollow>();
        InitializeRuntimeOffset();

        if (runtimeFollowOffset != position)
        {
            ClipPlaneAndOffsetLerper(position);
        }
    }

    void Orthographic(bool isOrthographic)
    {
        PostProcessingManager.Instance.Orthographic(isOrthographic);
        vCam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        vCam.Lens.OrthographicSize = 1300;

        Transform LookAtTarget = isOrthographic ? new GameObject().transform : playerFollowTarget;
        vCam.LookAt = LookAtTarget;
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
        transposer = vCam.GetComponent<CinemachineFollow>();
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
        transposer = vCam.GetComponent<CinemachineFollow>();
        InitializeRuntimeOffset();

        while (runtimeFollowOffset.z <= CloseCamDistance)
        {
            SetRuntimeFollowOffset(runtimeFollowOffset + Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward);
            yield return null;
        }

        SetRuntimeFollowOffset(new Vector3(0, 0, CloseCamDistance));
    }
}