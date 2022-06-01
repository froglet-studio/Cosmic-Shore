using UnityEngine;
using Cinemachine;
using StarWriter.Core;
using Amoebius.Utility.Singleton;

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

    private void OnEnable()
    {
        IntensitySystem.gameOver += ZoomEndCameraToScores;
        GameManager.onPlayGame += OnPlayGame;
    }

    private void OnDisable()
    {
        IntensitySystem.gameOver -= ZoomEndCameraToScores;
        GameManager.onPlayGame -= OnPlayGame;
    }

    void Start()
    {
        OnMainMenu();
        playerFollowTarget = endCameraLookAtTarget; // just so not null -- TODO: probably a better way to do whatever this is protecting against
        closeCamera.LookAt = farCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = playerFollowTarget;
    }
    private void OnMainMenu()
    {
        SetMainMenuCameraActive();
    }

    private void OnPlayGame()
    {
        playerFollowTarget = GameObject.FindGameObjectWithTag("Player").transform;
        closeCamera.LookAt = farCamera.LookAt = playerFollowTarget;
        closeCamera.Follow = farCamera.Follow = playerFollowTarget;
        SetFarCameraActive();
    }

    private void ZoomEndCameraToScores()
    {
        SetActiveCamera(endCamera);
    }
    public void SetMainMenuCameraActive()
    {
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

    private void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
    }
}
