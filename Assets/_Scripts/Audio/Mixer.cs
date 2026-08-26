using UnityEngine;
using FMODUnity;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;


namespace CosmicShore
{
    public class Mixer : MonoBehaviour
    {
        private FMOD.Studio.VCA VcaController;
        public string VCA;
       
        private Slider slider;



        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            VcaController = FMODUnity.RuntimeManager.GetVCA("vca:/" + VCA);
            slider = GetComponent<Slider>();
        }

        public void SetVolume(float volume)
        {
            VcaController.setVolume(volume);
        }
    }
}
