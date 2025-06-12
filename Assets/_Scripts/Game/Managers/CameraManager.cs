using Unity.Cinemachine;
using CosmicShore.Core;
using CosmicShore.Utility;
using System.Collections;
using UnityEngine;
using CosmicShore;
using CosmicShore.Utilities;

public class CameraManager : SingletonPersistent<CameraManager>
{
    [SerializeField] CinemachineCamera mainMenuCamera;
    [SerializeField] CinemachineVirtualCameraBase playerCamera;
    [SerializeField] CinemachineVirtualCameraBase deathCamera;
    [SerializeField] CinemachineVirtualCameraBase endCamera;
    
    [SerializeField] Transform endCameraFollowTarget;
    [SerializeField] Transform endCameraLookAtTarget;

    Transform playerFollowTarget;
    readonly int activePriority = 10;
    readonly int inactivePriority = 1;

    // Drift stuff
    bool zoomingOut;

    public bool FollowOverride = false;
    public bool FixedFollow = false; // If true, the camera will not follow the player, but will stay at a fixed position.
    public bool isOrthographic = false;

    public float CloseCamDistance;
    public float FarCamDistance;

    CinemachineCamera vCam;
    CinemachineFollow transposer;

    Coroutine zoomOutCoroutine;
    Coroutine returnToNeutralCoroutine;
    Coroutine lerper;

    private void OnEnable()
    {
        GameManager.OnPlayGame += SetupGamePlayCameras;
        GameManager.OnGameOver += SetEndCameraActive;
    }

    void OnDisable()
    {
        GameManager.OnPlayGame -= SetupGamePlayCameras;
        GameManager.OnGameOver -= SetEndCameraActive;
    }

    void Start()
    {
        vCam = playerCamera.gameObject.GetComponent<CinemachineCamera>();
        OnMainMenu();
    }

    public Transform GetCloseCamera()
    {
        return playerCamera.transform;
    }

    public void OnMainMenu()
    {
        SetMainMenuCameraActive();
        ThemeManager.Instance.SetBackgroundColor(Camera.main);
    }

    public void SetupGamePlayCameras()
    {
        playerFollowTarget = FollowOverride ? Hangar.Instance.SelectedShip.ShipStatus.ShipCameraCustomizer.FollowTarget : Hangar.Instance.SelectedShip.Transform;
        SetupGamePlayCameras(playerFollowTarget);
    }

    public void SetupGamePlayCameras(Transform _transform)
    {
        playerFollowTarget = _transform;
        playerCamera.LookAt = deathCamera.LookAt = playerFollowTarget;
        playerCamera.Follow = deathCamera.Follow = playerFollowTarget;
        ThemeManager.Instance.SetBackgroundColor(Camera.main);

        SetCloseCameraActive();
    }

    public void SetMainMenuCameraActive()
    {
        SetActiveCamera(mainMenuCamera);
        Invoke("LookAtCrystal", 1); // Delay to allow the old crystal to be destroyed
    }

    void LookAtCrystal()
    {
        mainMenuCamera.LookAt = NodeControlManager.Instance.GetNearestNode(Vector3.zero).GetCrystal().transform;
    }

    public void SetCloseCameraActive()
    {
        SetActiveCamera(playerCamera);
    }
    public void SetDeathCameraActive()
    {
        SetActiveCamera(deathCamera);
    }
    public void SetEndCameraActive()
    {
        SetActiveCamera(endCamera);
    }

    public void SetFixedFollowOffset(Vector3 offset)
    {
        FixedFollow = true;
        StartCoroutine(SetFollowOffsetCoroutine(offset));
    }

    IEnumerator SetFollowOffsetCoroutine(Vector3 offset)
    {
        yield return new WaitForSeconds(1); // Allow time for camera to stabilize
        transposer = vCam.GetComponent<CinemachineFollow>();
        transposer.FollowOffset = offset;
    }

    void SetActiveCamera(CinemachineVirtualCameraBase activeCamera)
    {
        Orthographic(isOrthographic);
        Debug.Log($"SetActiveCamera {activeCamera.Name}");

        mainMenuCamera.Priority = inactivePriority;
        playerCamera.Priority = inactivePriority;
        endCamera.Priority = inactivePriority;
        deathCamera.Priority = inactivePriority;

        activeCamera.Priority = activePriority;
        transposer = vCam.GetComponent<CinemachineFollow>();
        if (activeCamera == playerCamera) SetOffsetPosition(transposer.FollowOffset);
    }

    void ClipPlaneAndOffsetLerper(float normalizedDistance)
    {
        float CloseCamClipPlane = .5f;
        float FarCamClipPlane = .7f;
        if (lerper != null) 
            StopCoroutine(lerper);
        
        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine((transposer.FollowOffset.z - CloseCamDistance) / (FarCamDistance - CloseCamDistance),
            normalizedDistance, 1.5f, (i) =>
            {
                vCam.Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                transposer.FollowOffset = new Vector3(0, 0, (FarCamDistance - CloseCamDistance) * i + CloseCamDistance);
            }));
    }
    
    void ClipPlaneAndOffsetLerper(Vector3 offsetPosition)
    {
        float CloseCamClipPlane = .5f;
        //float FarCamClipPlane = .7f;
        if (lerper != null) 
            StopCoroutine(lerper);
        
        lerper = StartCoroutine(LerpUtilities.LerpingCoroutine(transposer.FollowOffset,
            offsetPosition, 1.5f, (i) =>
            {
                //vCam.Lens.NearClipPlane = (FarCamClipPlane - CloseCamClipPlane) * i + CloseCamClipPlane;
                transposer.FollowOffset = i;
            }));
    }

    public void SetNormalizedCloseCameraDistance(float normalizedDistance)
    {
        if (FixedFollow) return;
        transposer = vCam.GetComponent<CinemachineFollow>();

        if (transposer.FollowOffset != new Vector3(0, 0, normalizedDistance))
        {
            ClipPlaneAndOffsetLerper(normalizedDistance);
        }  
    }
    public void SetOffsetPosition(Vector3 position)
    {
        //offsetVector = position;
        transposer = vCam.GetComponent<CinemachineFollow>();

        if (transposer.FollowOffset != position)
        {
            ClipPlaneAndOffsetLerper(position);
        }  
    }

    void Orthographic(bool isOrthographic)
    {
        PostProcessingManager.Instance.Orthographic(isOrthographic);
        vCam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        vCam.Lens.OrthographicSize = 1300;

        Transform LookAtTarget = isOrthographic ? new GameObject().transform : playerFollowTarget;
        vCam.LookAt = LookAtTarget;
    }

    public void ZoomCloseCameraOut(float growthRate)
    {
        if (FixedFollow) return;
        if (returnToNeutralCoroutine != null)
        {
            StopCoroutine(returnToNeutralCoroutine);
            returnToNeutralCoroutine = null;
        }
        zoomingOut = true;
        zoomOutCoroutine = StartCoroutine(ZoomOutCloseCameraCoroutine(growthRate));
    }

    IEnumerator ZoomOutCloseCameraCoroutine(float growthRate)
    {
        transposer = vCam.GetComponent<CinemachineFollow>();

        while (zoomingOut && transposer.FollowOffset.z > FarCamDistance)
        {
            transposer.FollowOffset += Time.deltaTime * Mathf.Abs(growthRate) * -Vector3.forward;         
            yield return null;
        }
    }

    public void ResetCloseCameraToNeutral(float shrinkRate)
    {
        if (FixedFollow) return;
        if (zoomOutCoroutine != null)
        {
            StopCoroutine(zoomOutCoroutine);
            zoomOutCoroutine = null;
        }
        zoomingOut = false;
        returnToNeutralCoroutine = StartCoroutine(ReturnCloseCameraToNeutralCoroutine(shrinkRate));
    }

    IEnumerator ReturnCloseCameraToNeutralCoroutine(float shrinkRate)
    {
        
        transposer = vCam.GetComponent<CinemachineFollow>();

        while (transposer.FollowOffset.z <= CloseCamDistance)
        {
            transposer.FollowOffset += Time.deltaTime * Mathf.Abs(shrinkRate) * Vector3.forward;
            yield return null;
        }

        transposer.FollowOffset = new Vector3(0, 0, CloseCamDistance);
    }
}