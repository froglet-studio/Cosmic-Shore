using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Amoebius.Utility.Singleton;
using StarWriter.Core.Audio;
using System;
using UnityEngine.UI;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    public class GameManager : SingletonPersistent<GameManager>
    {
               
        [SerializeField]
        private bool skipTutorial = false;

        [SerializeField]
        private bool isGyroEnabled = true;

        private GameSetting gameSettings;

       

        // Start is called before the first frame update
        void Start()
        {
            gameSettings = GameSetting.Instance;

           if(PlayerPrefs.GetInt("Skip Tutorial") == 1) // 0 false and 1 true
            {
                skipTutorial = true;
            }
                      
        }

        public void OnClickTutorialToggleButton()
        {
            // Set gameSettings Tutorial status
            gameSettings.TutorialEnabled = !gameSettings.TutorialEnabled;

            // Set PlayerPrefs Tutorial status
            if (gameSettings.TutorialEnabled == true)
            {
                PlayerPrefs.SetInt("tutorialEnabled", 1);  //tutorial enabled
            }
            if (gameSettings.TutorialEnabled == false)
            {
                PlayerPrefs.SetInt("tutorialEnabled", 0);  //tutorial disabled
            }
        }

        public void OnClickGyroToggleButton()
        {
            // Set gameSettings Gyro status
            gameSettings.GyroEnabled = isGyroEnabled= !isGyroEnabled;

            // Set PlayerPrefs Gyro status
            if (isGyroEnabled == true)
            {
                PlayerPrefs.SetInt("gyroEnabled", 1); //gyro enabled
            }
            if (!isGyroEnabled == false)
            {
                PlayerPrefs.SetInt("gyroEnabled", 0);  //gyro disabled
            }
        }

        public void OnClickPlayButton()
        {
            if (!skipTutorial)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                SceneManager.LoadScene(2);
            }
            
        }

        public void OnClickResumeButton()
        {
            TogglePauseGame();
        }

        public void TogglePauseGame()
        {
            PauseSystem.TogglePauseGame();
        }

    }
}

