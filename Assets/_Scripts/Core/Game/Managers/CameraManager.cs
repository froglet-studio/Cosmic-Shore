using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System;
using StarWriter.Core;

public class CameraManager : SingletonPersistant<CameraManager>
{
    [SerializeField]
    private CinemachineVirtualCameraBase mainMenuCamera;
    [SerializeField]
    private CinemachineVirtualCameraBase closeCamera;
    [SerializeField]
    private CinemachineVirtualCameraBase farCamera;
    [SerializeField]
    private CinemachineVirtualCameraBase endCamera;

    
    private Transform playerFollowTarget;
    [SerializeField]
    private Transform endCameraFollowTarget;
    [SerializeField]
    private Transform endCameraLookAtTarget;

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

    // Start is called before the first frame update
    void Start()
    {
        OnMainMenu();
        playerFollowTarget = endCameraLookAtTarget; // just so not null
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
        mainMenuCamera.Priority = inactivePriority;
        endCamera.Priority = activePriority;
        farCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
    }
    public void SetMainMenuCameraActive()
    {
        mainMenuCamera.Priority = activePriority;
        farCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
    }
    public void SetFarCameraActive()
    {
        mainMenuCamera.Priority = inactivePriority;
        farCamera.Priority = activePriority;
        closeCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
    }

    public void SetCloseCameraActive()
    {
        mainMenuCamera.Priority = inactivePriority;
        closeCamera.Priority = activePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
    }

}
