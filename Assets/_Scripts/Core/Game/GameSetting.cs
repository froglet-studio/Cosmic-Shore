using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace StarWriter.Core
{
    public class GameSetting : MonoBehaviour
{
    //floats
    private float masterVolume = 0.5f; //TODO 
    private float currentVolume;
    //bools
    private bool isMuted = false;

    private void Start()
    {
        currentVolume = masterVolume;
    }


    public void ChangeMasterVolume(float amount)
    {
            if (!isMuted)
            {
                masterVolume += amount;
                masterVolume = Mathf.Clamp(masterVolume, 0, 1);
            }
        
    }

    public void ToggleMusic()
    {
        if (currentVolume < 0)
        {
            currentVolume = 0;
                isMuted = !isMuted;

        }

    }
}
}



