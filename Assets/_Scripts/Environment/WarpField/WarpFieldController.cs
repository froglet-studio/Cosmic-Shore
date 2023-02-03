using UnityEngine;
using StarWriter.Core.Input;

public class WarpFieldController : MonoBehaviour
{
    WarpFieldData warpFieldData;
    CameraManager cameraManager;
    Transform player;
    Transform blue;
    Transform red;
    InputController playerInput;

    void Start()
    {
        warpFieldData = GetComponent<WarpFieldData>();
        cameraManager = CameraManager.Instance;

        player = GameObject.FindGameObjectWithTag("Player").transform;
        blue = GameObject.FindGameObjectWithTag("blue").transform;
        red = GameObject.FindGameObjectWithTag("red").transform;

        playerInput = player.GetComponent<InputController>();
    }

    void Update()
    {
        var fieldResult = warpFieldData.HybridVector(player).magnitude;
        cameraManager.SetBothCameraDistances(-fieldResult); //TODO: set clip plane to half the distance
        playerInput.throttleScaler = playerInput.defaultThrottleScaler * fieldResult;
        playerInput.minimumSpeed = playerInput.defaultMinimumSpeed * fieldResult;

        player.localScale = Vector3.one * fieldResult;
        blue.localScale = Vector3.one * fieldResult;
        red.localScale = Vector3.one * fieldResult;
    }
}