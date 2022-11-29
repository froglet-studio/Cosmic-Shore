using System.Collections;
using System.Collections.Generic;
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
    AIPilot redInput;
    AIPilot blueInput;

    // Start is called before the first frame update
    void Start()
    {
        warpFieldData = GetComponent<WarpFieldData>();
        cameraManager = CameraManager.Instance;

        player = GameObject.FindGameObjectWithTag("Player").transform;
        blue = GameObject.FindGameObjectWithTag("blue").transform;
        red = GameObject.FindGameObjectWithTag("red").transform;

        playerInput = player.GetComponent<InputController>();
        redInput = red.GetComponent<AIPilot>();
        blueInput = blue.GetComponent<AIPilot>();
    }

    // Update is called once per frame
    void Update()
    {
        var fieldResult = warpFieldData.HybridVector(player).magnitude;
        cameraManager.SetCameraDistance(fieldResult);//todo set clip plane to half the distance
        playerInput.throttleScaler = playerInput.initialThrottleScaler * fieldResult;
        playerInput.defaultThrottle = playerInput.initialDThrottle * fieldResult;

        //redInput.throttle = 


        player.localScale = Vector3.one * fieldResult;
        blue.localScale = Vector3.one * fieldResult;
        red.localScale = Vector3.one * fieldResult;
    }
}
