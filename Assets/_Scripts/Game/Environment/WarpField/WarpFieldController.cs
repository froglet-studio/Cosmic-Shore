using UnityEngine;


// TODO: P1 remove this class
public class WarpFieldController : MonoBehaviour
{
    WarpFieldData warpFieldData;
    CameraManager cameraManager;
    Transform playerTransform;
    Transform blue;
    Transform red;
    ShipTransformer shipTransformer;

    void Start()
    {
        warpFieldData = GetComponent<WarpFieldData>();
        cameraManager = CameraManager.Instance;

        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        blue = GameObject.FindGameObjectWithTag("blue").transform;
        red = GameObject.FindGameObjectWithTag("red").transform;

        // get shipTransformer by other way!
        // shipTransformer = playerTransform.Ship.GetComponent<ShipTransformer>();
    }

    void Update()
    {
        var fieldResult = warpFieldData.HybridVector(playerTransform).magnitude;
        //cameraManager.SetBothCameraDistances(-fieldResult); //TODO: set clip plane to half the distance
        shipTransformer.ThrottleScaler = shipTransformer.DefaultThrottleScaler * fieldResult;
        shipTransformer.MinimumSpeed = shipTransformer.DefaultMinimumSpeed * fieldResult;

        playerTransform.localScale = Vector3.one * fieldResult;
        blue.localScale = Vector3.one * fieldResult;
        red.localScale = Vector3.one * fieldResult;
    }
}