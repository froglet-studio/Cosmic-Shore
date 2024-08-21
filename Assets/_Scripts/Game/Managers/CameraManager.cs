using Cinemachine;
using CosmicShore.Core;
using CosmicShore.Utility.Singleton;
using CosmicShore.Utility;
using System.Collections;
using UnityEngine;
using CosmicShore;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField] CinemachineVirtualCameraBase mainMenuCamera;
    [SerializeField] CinemachineVirtualCameraBase closeCamera;
    [SerializeField] CinemachineVirtualCameraBase deathCamera;
    [SerializeField] CinemachineVirtualCameraBase endCamera;
    
    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    // Drift stuff
    bool zoomingOut;

    public bool FollowOverride = false;
    public bool isOrthographic = false;

    public float CloseCamDistance;
    public float FarCamDistance;

    CinemachineVirtualCamera vCam;
    CinemachineTransposer transposer;

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
    }

    void Start()
    {
        vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();

        OnMainMenu();
    }

    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    public void SetupGamePlayCameras()
    {
        playerFollowTarget = FollowOverride ? Hangar.Instance.SelectedShip.ShipCameraCustomizer.FollowTarget : Hangar.Instance.SelectedShip.transform;
        closeCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = deathCamera.Follow = playerFollowTarget;
        
        SetCloseCameraActive();
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        playerFollowTarget = _transform;
        closeCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = deathCamera.Follow = playerFollowTarget;

        SetCloseCameraActive();
    }

    public void SetMainMenuCameraActive()
    {
        SetActiveCamera(mainMenuCamera);
    }
    public void SetCloseCameraActive()
    {
        SetActiveCamera(closeCamera);
    }
    public void SetDeathCameraActive()
    {
        SetActiveCamera(deathCamera);
    }
    public void SetEndCameraActive()
    {
        SetActiveCamera(endCamera);
    }

    void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        Orthographic(isOrthographic);
        Debug.Log($"SetActiveCamera {activeCamera.Name}");

        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
        deathCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;

        if (activeCamera == closeCamera) SetNormalizedCloseCameraDistance(0);
    }

    void ClipPlaneAndOffsetLerper(float normalizedDistance)
    {
        float CloseCamClipPlane = -CloseCamDistance / 5;
        float FarCamClipPlane = 5f;
        if (lerper != null) 
            StopCoroutine(lerper);
        
        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine((transposer.m_FollowOffset.z - CloseCamDistance) / (FarCamDistance - CloseCamDistance),
            normalizedDistance, 1.5f, (i) =>
            {
                vCam.m_Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                transposer.m_FollowOffset = new Vector3(0, 0, (FarCamDistance - CloseCamDistance) * i + CloseCamDistance);
            }));
    }

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();

        if (transposer.m_FollowOffset != new Vector3(0, 0, normalizedDistance))
        {
            ClipPlaneAndOffsetLerper(normalizedDistance);
        }  
    }

    void Orthographic(bool isOrthographic)
    {
        PostProcessingManager.Instance.Orthographic(isOrthographic);
        vCam.m_Lens.Orthographic = isOrthographic;
        vCam.m_Lens.OrthographicSize = 1300;

        Transform LookAtTarget = isOrthographic ? new GameObject().transform : playerFollowTarget;
        vCam.LookAt = LookAtTarget;
    }

    public void ZoomCloseCameraOut(float growthRate)
    {
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
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();

        while (zoomingOut && transposer.m_FollowOffset.z > FarCamDistance)
        {
            transposer.m_FollowOffset += Time.deltaTime * Mathf.Abs(growthRate) * -Vector3.forward;         
            yield return null;
        }
    }

    public void ResetCloseCameraToNeutral(float shrinkRate)
    {
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
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();

        while (transposer.m_FollowOffset.z <= CloseCamDistance)
        {
            transposer.m_FollowOffset += Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward;
            yield return null;
        }

        transposer.m_FollowOffset = new Vector3(0, 0, CloseCamDistance);
    }
}