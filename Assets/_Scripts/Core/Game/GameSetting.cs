using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amoebius.Core.Input;


public class GameSetting : MonoBehaviour
{
    private float masterVolume = 1; //TODO 

    private bool gyroStatus = true;  //Use for UI toggle value




    public void ChangeMasterVolume(float amount)
    {
        masterVolume += amount;
        masterVolume = Mathf.Clamp(masterVolume, 0, 1);
    }

    public void Mute()
    {
        masterVolume = 0;
    }

    public void ChangeGyroscopeEnabledStatus()
    {
        Gyro gyro = FindObjectOfType<Player>().GetComponent<Gyro>();
        gyroStatus = !gyroStatus;
        gyro.FlipUseGyro();
    }

    
}
