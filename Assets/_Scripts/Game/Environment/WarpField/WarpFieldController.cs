using UnityEngine;
using CosmicShore.Game.IO;

// TODO: P1 remove this class
public class WarpFieldController : MonoBehaviour
{
    WarpFieldData warpFieldData;
    CameraManager cameraManager;
    Transform player;
    Transform blue;
    Transform red;
    ShipTransformer shipController;

    void Start()
    {
        warpFieldData = GetComponent<WarpFieldData>();
        cameraManager = CameraManager.Instance;

        player = GameObject.FindGameObjectWithTag("Player").transform;
        blue = GameObject.FindGameObjectWithTag("blue").transform;
        red = GameObject.FindGameObjectWithTag("red").transform;

        shipController = player.GetComponent<InputController>().Ship.GetComponent<ShipTransformer>();
    }

    void Update()
    {
        var fieldResult = warpFieldData.HybridVector(player).magnitude;
        //cameraManager.SetBothCameraDistances(-fieldResult); //TODO: set clip plane to half the distance
        shipController.ThrottleScaler = shipController.DefaultThrottleScaler * fieldResult;
        shipController.MinimumSpeed = shipController.DefaultMinimumSpeed * fieldResult;

        player.localScale = Vector3.one * fieldResult;
        blue.localScale = Vector3.one * fieldResult;
        red.localScale = Vector3.one * fieldResult;
    }
}