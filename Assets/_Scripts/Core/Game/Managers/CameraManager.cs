using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System;

public class CameraManager : SingletonPersistant<CameraManager>
{
    [SerializeField]
    public CinemachineVirtualCamera closeCamera;
    [SerializeField]
    public CinemachineVirtualCamera farCamera;
    [SerializeField]
    public CinemachineVirtualCamera endCamera;

    [SerializeField]
    private Transform endCameraFollowTarget;
    [SerializeField]
    private Transform endCameraLookAtTarget;

    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    private void OnEnable()
    {
        IntensitySystem.gameOver += ZoomEndCameraToScores;
    }

    private void OnDisable()
    {
        IntensitySystem.gameOver += ZoomEndCameraToScores;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void ZoomEndCameraToScores()
    {
        endCamera.Priority = activePriority;
        farCamera.Priority = inactivePriority;
        closeCamera.Priority = inactivePriority;
    }

    public void SetFarCameraActive()
    {
        farCamera.Priority = activePriority;
        closeCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
    }

    public void SetCloseCameraActive()
    {
        closeCamera.Priority = activePriority;
        farCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
    }

}
