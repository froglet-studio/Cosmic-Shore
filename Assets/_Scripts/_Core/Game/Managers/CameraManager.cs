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

    private Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    private bool isCameraFlipEnabled = true;
    private bool useCloseCam = true;

    [SerializeField] float closeCamDistance = 4f;
    [SerializeField] float farCamDistance = 50f;

    WarpFieldData warpFieldData;

    private void OnEnable()
    {
        GameManager.onPhoneFlip += ToggleCloseOrFarCamOnPhoneFlip;
        GameManager.onPlayGame += SetupGamePlayCameras;
        DeathEvents.OnDeathBegin += SwitchToDeathCamera;
        GameManager.onExtendGamePlay += SwitchToGamePlayCameras;
        GameManager.onGameOver += SwitchToEndCamera;
    }

    private void OnDisable()
    {
        GameManager.onPhoneFlip -= ToggleCloseOrFarCamOnPhoneFlip;
        GameManager.onPlayGame -= SetupGamePlayCameras;
        DeathEvents.OnDeathBegin -= SwitchToDeathCamera;
        GameManager.onExtendGamePlay -= SwitchToGamePlayCameras;
        GameManager.onGameOver -= SwitchToEndCamera;
    }

    void Start()
    {
        OnMainMenu();
    }

    void Update()
    {
        warpFieldData = GetComponent<WarpFieldData>();
    }

    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    private void SetupGamePlayCameras()
    {
        isCameraFlipEnabled = true;

        playerFollowTarget = GameObject.FindGameObjectWithTag("Player").transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;
        SetCloseCameraActive();
    }

    private void SwitchToDeathCamera()
    {
        isCameraFlipEnabled = false;

        SetDeathCameraActive();
    }

    private void SwitchToEndCamera()
    {
        isCameraFlipEnabled = false;
        SetEndCameraActive();
    }

    private void SwitchToGamePlayCameras()
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
    private void SetDeathCameraActive()
    {
        SetActiveCamera(deathCamera);
    }
    private void SetEndCameraActive()
    {
        SetActiveCamera(endCamera);
    }

    private void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
        deathCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
    }

    private void ToggleCloseOrFarCamOnPhoneFlip(bool state)
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
    
    public void SetCameraDistance(float distance)
    {
        var vCam = closeCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        var transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0,0,-distance * closeCamDistance);

        vCam = farCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>();
        transposer = vCam.GetCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0, 0, -distance * farCamDistance);
    }
}