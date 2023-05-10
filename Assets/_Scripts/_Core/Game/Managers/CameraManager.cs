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

    void SwitchToGamePlayCameras()
    {
        isCameraFlipEnabled = true;

        if (useCloseCam)
            SetCloseCameraActive();
        else
            SetFarCameraActive();
    }

    public void SetMainMenuCameraActive()
    {
        isCameraFlipEnabled = false;

        SetActiveCamera(mainMenuCamera);
    }

    public void SetFarCameraActive()
    {
        SetActiveCamera(farCamera);
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

    public void ToggleCloseOrFarCamOnPhoneFlip(bool state)
    {
        if (isCameraFlipEnabled)
        {
            useCloseCam = state;

            if (useCloseCam)
                SetCloseCameraActive();
            else       
                SetFarCameraActive();
        }
    }

    void SetCameraDistance(CinemachineVirtualCameraBase camera, float distance)
    {
        var vCam = camera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0, 0, distance);
    }

    Coroutine setCameraDistanceCoroutine;
    IEnumerator SetCameraDistanceCoroutine(CinemachineVirtualCameraBase camera, float distance)
    {
        var vCam = camera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        while (transposer.m_FollowOffset != new Vector3(0, 0, distance))
        {
            float lerpRate;
            if (transposer.m_FollowOffset.z < distance)
                lerpRate = .006f;
            else 
                lerpRate = .002f;

            transposer.m_FollowOffset = Vector3.Lerp(transposer.m_FollowOffset, new Vector3(0, 0, distance), lerpRate);
            vCam.m_Lens.NearClipPlane = Mathf.Lerp(vCam.m_Lens.NearClipPlane, -distance / 5, lerpRate);
            yield return null;
        }
    }

    Coroutine lerper;
    void DistanceLerper(float newDistanceScalar)
    {
        if (lerper != null) StopCoroutine(lerper);
        lerper = StartCoroutine(Tools.LerpingCoroutine((i) => { transposer.m_FollowOffset = new Vector3(0, 0, i); }, () => transposer.m_FollowOffset.z, newDistanceScalar, 4f, 1000));
    }

    public void SetFarCameraDistance(float distance)
    {
        SetCameraDistance(farCamera, distance);
    }

    public void SetCloseCameraDistance(float distance) 
    {
        var vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        //if (setCameraDistanceCoroutine != null) 
        //    StopCoroutine(setCameraDistanceCoroutine);
        if (transposer.m_FollowOffset != new Vector3(0, 0, distance))
            DistanceLerper(distance);
            //setCameraDistanceCoroutine = StartCoroutine(SetCameraDistanceCoroutine(closeCamera, distance));
    }

    public void SetBothCameraDistances(float distance)
    {
        SetCameraDistance(farCamera, distance);
        SetCameraDistance(closeCamera, distance);
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