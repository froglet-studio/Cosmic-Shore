using UnityEngine;
using Cinemachine;
using StarWriter.Core;
using TailGlider.Utility.Singleton;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField]
    private CinemachineVirtualCameraBase mainMenuCamera;
    
    [SerializeField]
    private CinemachineVirtualCameraBase closeCamera;
    
    [SerializeField]
    private CinemachineVirtualCameraBase farCamera;
    
    [SerializeField]
    private CinemachineVirtualCameraBase endCamera;
    
    [SerializeField]
    private Transform endCameraFollowTarget;
    
    [SerializeField]
    private Transform endCameraLookAtTarget;

    private Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    private bool isCameraFlipEnabled = true;

    private void OnEnable()
    {
        
        GameManager.onPlayGame += OnPlayGame;
        GameManager.onPhoneFlip += OnPhoneFlip;
}

    private void OnDisable()
    {
        
        GameManager.onPlayGame -= OnPlayGame;
        GameManager.onPhoneFlip -= OnPhoneFlip;
    }

    void Start()
    {
        OnMainMenu();
        playerFollowTarget = endCameraLookAtTarget; // just so not null -- TODO: probably a better way to do whatever this is protecting against
        closeCamera.LookAt = farCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = playerFollowTarget;
    }
    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    private void OnPlayGame()
    {
        FuelSystem.zeroFuel += ZoomEndCameraToScores;
        playerFollowTarget = GameObject.FindGameObjectWithTag("Player").transform;
        closeCamera.LookAt = farCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = playerFollowTarget;
        SetFarCameraActive();
        isCameraFlipEnabled = true;
    }

    private void ZoomEndCameraToScores()
    {
        FuelSystem.zeroFuel -= ZoomEndCameraToScores;
        SetActiveCamera(endCamera);
        isCameraFlipEnabled = false;
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

    private void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
    }

    private void OnPhoneFlip(bool state)
    {
        {
            if (isCameraFlipEnabled && state)
            {
                SetCloseCameraActive();
            }
            else if (isCameraFlipEnabled)
            {
                SetFarCameraActive();
            }

        }
    }
}