using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.Input;

public class TutorialPlayerController : MonoBehaviour
{
    public Dictionary<string, bool> controlLevels = new Dictionary<string,bool>();

    [SerializeField]
    private TutorialInputController inputController;

    private void InitializeControlLevels() //Adding Test Names and setting bools false
    {
        controlLevels.Add("Pitch Up", false);
        controlLevels.Add("Pitch Down", false);
        controlLevels.Add("Yaw Left", false);
        controlLevels.Add("Yaw Right", false);
        controlLevels.Add("Roll Left", false);
        controlLevels.Add("Roll Right", false);
        controlLevels.Add("throttleScaler Up", false);
        controlLevels.Add("Slow Down", false);
        controlLevels.Add("Gyro", false);

    }

    // Start is called before the first frame update
    void Start()
    {
        InitializeControlLevels();
        controlLevels["Pitch Up"] = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (controlLevels["Pitch Up"] == true)
        {
            PitchUp();
        }
        if (controlLevels["Pitch Down"] == true)
        {
            PitchDown();
        }
        if (controlLevels["Yaw Left"] == true)
        {
            YawLeft();
        }
        if (controlLevels["Yaw Right"] == true)
        {
            YawRight();
        }
        if (controlLevels["Roll Left"] == true)
        {
            RollLeft();
        }
        if (controlLevels["Roll Right"] == true)
        {
            RollRight();
        }
        if (controlLevels["throttleScaler Up"] == true)
        {
            SpeedUp();
        }
        if (controlLevels["Slow Down"] == true)
        {
            SlowDown();
        }
        if (controlLevels["Gyro"] == true)
        {
            Gyro();
        }
    }
    public void PitchUp()
    {
        Debug.Log("pitch up");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Pitch;
    }
    private void PitchDown()
    {
        Debug.Log("pitch down");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Pitch;
    }
    private void YawLeft()
    {
        Debug.Log("yaw left");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Yaw;
    }
    private void YawRight()
    {
        Debug.Log("yaw right");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Yaw;
    }

    private void RollLeft()
    {
        Debug.Log("roll left");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Roll;
    }
    private void RollRight()
    {
        Debug.Log("roll right");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Roll;
    }

    private void SpeedUp()
    {
        Debug.Log("speed up");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Throttle;
    }
    private void SlowDown()
    {
        Debug.Log("Slow Down");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Throttle;
    }
    private void Gyro()
    {
        Debug.Log("gyro");
        inputController.flightControlScheme = TutorialInputController.ControlScheme.Gyro;
    }

    

    

    

    

    

    
}
