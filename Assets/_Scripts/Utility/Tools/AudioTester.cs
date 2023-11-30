using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class AudioTester : MonoBehaviour
    {
        [SerializeField] AudioSource audioSource;

        // Start is called before the first frame update
        void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Update is called once per frame
        void OnClickPlayAudioTest()
        {
            
        }
    }
}
