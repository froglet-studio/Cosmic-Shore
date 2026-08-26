using UnityEngine;
using FMODUnity;

namespace CosmicShore
{
    public class AudioManager : MonoBehaviour
    {
        [SerializeField] EventReference music;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            PlayMusic();
        }

        public void PlayMusic()
        {
            RuntimeManager.PlayOneShot(music);
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
