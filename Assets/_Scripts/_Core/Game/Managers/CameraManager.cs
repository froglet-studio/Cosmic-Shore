using UnityEngine;
using Cinemachine;
using StarWriter.Core;
using TailGlider.Utility.Singleton;
using System.Collections;
using StarWriter.Utility.Tools;
using StarWriter.Core.HangerBuilder;


public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField] CinemachineVirtualCameraBase mainMenuCamera;
    [SerializeField] CinemachineVirtualCameraBase closeCamera;
    [SerializeField] CinemachineVirtualCameraBase farCamera;
    [SerializeField] CinemachineVirtualCameraBase deathCamera;
    [SerializeField] CinemachineVirtualCameraBase endCamera;
    
    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    bool isCameraFlipEnabled = true;
    bool useCloseCam = true;

    // Drift stuff
    public float distanceScaler = 1f;
    public Vector3 tempOffset = Vector3.zero;
    public bool ZoomingOut;

    public float CloseCamDistance;
    public float FarCamDistance;
    float targetDistance;
    CinemachineVirtualCamera vCam;
    CinemachineTransposer transposer;

    private void OnEnable()
    {
        GameManager.onPlayGame += SetupGamePlayCameras;
        DeathEvents.OnDeathBegin += SwitchToDeathCamera;
        GameManager.onGameOver += SwitchToEndCamera;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= SetupGamePlayCameras;
        DeathEvents.OnDeathBegin -= SwitchToDeathCamera;
        GameManager.onGameOver -= SwitchToEndCamera;
    }

    void Start()
    {
        OnMainMenu();
    }

    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    public void SetupGamePlayCameras()
    {
        isCameraFlipEnabled = true;

        playerFollowTarget = Hangar.Instance.SelectedShip.transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;
        
        SetCloseCameraActive();
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        isCameraFlipEnabled = true;

        playerFollowTarget = _transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;

        SetCloseCameraActive();
    }

    void SwitchToDeathCamera()
    {
        isCameraFlipEnabled = false;

        SetDeathCameraActive();
    }

    void SwitchToEndCamera()
    {
        isCameraFlipEnabled = false;
        SetEndCameraActive();
    }

    public void SetMainMenuCameraActive()
    {
        isCameraFlipEnabled = false;

        SetActiveCamera(mainMenuCamera);
    }

    public void SetCloseCameraActive()
    {
        SetActiveCamera(closeCamera);
    }
    void SetDeathCameraActive()
    {
        SetActiveCamera(deathCamera);
    }
    void SetEndCameraActive()
    {
        SetActiveCamera(endCamera);
    }

    void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
        deathCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
        vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
    }

    Coroutine lerper;

    void ClipPlaneAndOffsetLerper(float normalizedDistance)
    {
        float CloseCamClipPlane = -CloseCamDistance / 5;
        float FarCamClipPlane = 5f;
        if (lerper != null) StopCoroutine(lerper);
        lerper = StartCoroutine(Tools.LerpingCoroutine((transposer.m_FollowOffset.z - CloseCamDistance) / (FarCamDistance - CloseCamDistance),
            normalizedDistance, 1.5f, (i) =>
            {
                vCam.m_Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                transposer.m_FollowOffset = new Vector3(0, 0, (FarCamDistance - CloseCamDistance) * i + CloseCamDistance);
            }));
    }

    public void SetNormalizedCameraDistance(float normalizedDistance)
    {
        if (transposer.m_FollowOffset != new Vector3(0, 0, normalizedDistance))
        {
            ClipPlaneAndOffsetLerper(normalizedDistance);
        }  
    }

    public void ZoomOut(float growthRate)
    {
        if (returnToNeutralCoroutine != null)
        {
            StopCoroutine(returnToNeutralCoroutine);
            returnToNeutralCoroutine = null;
        }
        ZoomingOut = true;
        zoomOutCoroutine = StartCoroutine(ZoomOutCoroutine(growthRate));
    }

    IEnumerator ZoomOutCoroutine(float growthRate)
    {
        var vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        while (ZoomingOut && transposer.m_FollowOffset.z > FarCamDistance)
        {
            transposer.m_FollowOffset += Time.deltaTime * Mathf.Abs(growthRate) * -Vector3.forward;         
            yield return null;
        }
    }

    public void ResetToNeutral(float shrinkRate)
    {
        if (zoomOutCoroutine != null)
        {
            StopCoroutine(zoomOutCoroutine);
            zoomOutCoroutine = null;
        }
        ZoomingOut = false;
        returnToNeutralCoroutine = StartCoroutine(ReturnToNeutralCoroutine(shrinkRate));
    }

    Coroutine zoomOutCoroutine;
    Coroutine returnToNeutralCoroutine;

    IEnumerator ReturnToNeutralCoroutine(float shrinkRate)
    {
        var vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        while (transposer.m_FollowOffset.z <= CloseCamDistance)
        {
            transposer.m_FollowOffset += Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward;
            yield return null;
        }
        transposer.m_FollowOffset = new Vector3(0,0,CloseCamDistance);
        //SetCloseCameraDistance(CloseCamDistance);
    }
}