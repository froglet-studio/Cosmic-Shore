using Cinemachine;
using StarWriter.Core;
using StarWriter.Core.HangerBuilder;
using StarWriter.Utility.Singleton;
using StarWriter.Utility.Tools;
using System.Collections;
using UnityEngine;

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

    // Drift stuff
    bool zoomingOut;

    public float CloseCamDistance;
    public float FarCamDistance;
    CinemachineVirtualCamera vCam;
    CinemachineTransposer transposer;

    private void OnEnable()
    {
        GameManager.onPlayGame += SetupGamePlayCameras;
        DeathEvents.OnDeathBegin += SetDeathCameraActive;
        GameManager.onGameOver += SetEndCameraActive;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= SetupGamePlayCameras;
        DeathEvents.OnDeathBegin -= SetDeathCameraActive;
        GameManager.onGameOver -= SetEndCameraActive;
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
        playerFollowTarget = Hangar.Instance.SelectedShip.transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;
        
        SetCloseCameraActive();
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        playerFollowTarget = _transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;

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
        if (lerper != null) 
            StopCoroutine(lerper);
        
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
        zoomingOut = true;
        zoomOutCoroutine = StartCoroutine(ZoomOutCoroutine(growthRate));
    }

    IEnumerator ZoomOutCoroutine(float growthRate)
    {
        var vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        while (zoomingOut && transposer.m_FollowOffset.z > FarCamDistance)
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
        zoomingOut = false;
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
    }
}