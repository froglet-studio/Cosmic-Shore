using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amoebius.Utility.Singleton;
using StarWriter.Core.Audio;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(GameSetting))]
    public class GameManager : SingletonPersistent<GameManager>
    {
        [SerializeField]
        private AudioClip backgroundMusic;
        [SerializeField]
        private GameSetting gameSettings;

        [SerializeField]
        private AudioManager audioManager;

        //TODO get AudioMaster dl
        //set background music volume
        // play music

        // Start is called before the first frame update
        void Start()
        {
            
            audioManager.PlayMusicClip(backgroundMusic);
            audioManager.SetMusicVolume(0.1f);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

