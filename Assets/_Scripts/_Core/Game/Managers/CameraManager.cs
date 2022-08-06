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

    private void OnEnable()
    {
        GameManager.onPhoneFlip += ToggleCloseOrFarCamOnPhoneFlip;
        GameManager.onPlayGame += SetupGamePlayCameras;
        GameManager.onDeath += SwitchToDeathCamera;
        GameManager.onExtendGamePlay += SwitchToGamePlayCameras;
        GameManager.onGameOver += SwitchToEndCamera;
    }

    private void OnDisable()
    {
        GameManager.onPhoneFlip -= ToggleCloseOrFarCamOnPhoneFlip;
        GameManager.onPlayGame -= SetupGamePlayCameras;
        GameManager.onDeath -= SwitchToDeathCamera;
        GameManager.onExtendGamePlay -= SwitchToGamePlayCameras;
        GameManager.onGameOver -= SwitchToEndCamera;
    }

    void Start()
    {
        OnMainMenu();
        playerFollowTarget = endCameraLookAtTarget; // just so not null -- TODO: probably a better way to do whatever this is protecting against
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;
    }
    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    private void SetupGamePlayCameras()
    {
        playerFollowTarget = GameObject.FindGameObjectWithTag("Player").transform;
        closeCamera.LookAt = farCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = deathCamera.Follow = playerFollowTarget;
        SetCloseCameraActive();
        isCameraFlipEnabled = true;
    }

    private void SwitchToDeathCamera()
    {
        // TODO: is the death camera still a thing?
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
        SetActiveCamera(mainMenuCamera);
        isCameraFlipEnabled = false;

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
}