using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.Input;

public class TutorialPlayerController : MonoBehaviour
{
    public Dictionary<string, bool> controlLevels = new Dictionary<string,bool>();

    [SerializeField]
    private InputController inputController;

    private void InitializeControlLevels() //Adding Test Names and setting bools false
    {
        controlLevels.Add("Pitch Up", false);
        controlLevels.Add("Pitch Down", false);
        controlLevels.Add("Yaw Left", false);
        controlLevels.Add("Yaw Right", false);
        controlLevels.Add("Roll Left", false);
        controlLevels.Add("Roll Right", false);
        controlLevels.Add("Speed Up", false); 
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
            EnablePitch();
        }
        if (controlLevels["Pitch Down"] == true)
        {
            EnablePitch();
        }
        if (controlLevels["Yaw Left"] == true)
        {
            EnableYaw();
        }
        if (controlLevels["Yaw Right"] == true)
        {
            EnableYaw();
        }
        if (controlLevels["Roll Left"] == true)
        {
            EnableRoll();
        }
        if (controlLevels["Roll Right"] == true)
        {
            EnableRoll();
        }
        if (controlLevels["Speed Up"] == true)
        {
            EnableThrottle();
        }
        if (controlLevels["Slow Down"] == true)
        {
            EnableThrottle();
        }
        if (controlLevels["Gyro"] == true)
        {
            EnableGyro();
        }
    }
    public void EnablePitch()
    {
        Debug.Log("pitch up");
        inputController.IsPitchEnabled = true;

        inputController.IsYawEnabled = false;
        inputController.IsRollEnabled = false;
        inputController.IsThrottleEnabledl = true;
        inputController.IsGyroEnabled = false;
    }
    private void EnableYaw()
    {
        Debug.Log("yaw left");
        inputController.IsYawEnabled = true;

        inputController.IsPitchEnabled = false;
        inputController.IsRollEnabled = false;
        inputController.IsThrottleEnabledl = true;
        inputController.IsGyroEnabled = false;
    }

    private void EnableRoll()
    {
        Debug.Log("roll left");
        inputController.IsRollEnabled = true;

        inputController.IsPitchEnabled = false;
        inputController.IsYawEnabled = false;
        inputController.IsThrottleEnabledl = true;
        inputController.IsGyroEnabled = false;
        }

    private void EnableThrottle()
    {
        Debug.Log("speed up");
        inputController.IsThrottleEnabledl = true;

        inputController.IsPitchEnabled = false;
        inputController.IsYawEnabled = false;
        inputController.IsRollEnabled = false;
        inputController.IsGyroEnabled = false;
    }
    
    private void EnableGyro()
    {
        Debug.Log("gyro");
        inputController.IsGyroEnabled = true;

        inputController.IsPitchEnabled = false;
        inputController.IsYawEnabled = false;
        inputController.IsRollEnabled = false;
        inputController.IsThrottleEnabledl = false;
    }   
}
