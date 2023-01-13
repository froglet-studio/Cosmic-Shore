using UnityEngine;
using Cinemachine;
using StarWriter.Core;
using TailGlider.Utility.Singleton;

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
    public float driftDistance = 1f;
    public Vector3 tempOffset = Vector3.zero;

    private void OnEnable()
    {
        GameManager.onPlayGame += SetupGamePlayCameras;
        DeathEvents.OnDeathBegin += SwitchToDeathCamera;
        GameManager.onExtendGamePlay += SwitchToGamePlayCameras;
        GameManager.onGameOver += SwitchToEndCamera;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= SetupGamePlayCameras;
        DeathEvents.OnDeathBegin -= SwitchToDeathCamera;
        GameManager.onExtendGamePlay -= SwitchToGamePlayCameras;
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

    void SetupGamePlayCameras()
    {
        isCameraFlipEnabled = true;

        playerFollowTarget = GameObject.FindGameObjectWithTag("Player_Ship").transform;
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

    public void SetCameraDistance(CinemachineVirtualCameraBase camera, float distance)
    {
        var vCam = camera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0, 0, distance);
    }

    public void SetFarCameraDistance(float distance)
    {
        SetCameraDistance(farCamera, distance);
    }

    public void SetCloseCameraDistance(float distance) 
    {
        SetCameraDistance(closeCamera, distance);
    }

    public void SetBothCameraDistances(float distance)
    {
        SetCameraDistance(farCamera, distance);
        SetCameraDistance(closeCamera, distance);
    }

    public void DriftCam(Vector3 initialDirection, Vector3 currentRotation)
    {
        
        var vCam = farCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        if (tempOffset == Vector3.zero) tempOffset = transposer.m_FollowOffset;
        transposer.m_FollowOffset = Quaternion.FromToRotation(currentRotation, initialDirection) * tempOffset * driftDistance;
        driftDistance += 0f;
    }
}