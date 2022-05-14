using System.Collections;
using System.Collections.Generic;
using Amoebius.Utility.Singleton;
using UnityEngine;


namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {

        #region Settings
        [SerializeField]
        private bool isMuted = false;
        [SerializeField]
        private bool tutorialEnabled = true;
        [SerializeField]
        private bool hasCompletedTutorial = false;
        [SerializeField]
        private bool gyroEnabled = true;

        public bool IsMuted { get => isMuted; set => isMuted = value; }
        public bool TutorialEnabled { get => tutorialEnabled; set => tutorialEnabled = value; }
        public bool HasCompletedTutorial { get => hasCompletedTutorial; set => hasCompletedTutorial = value; }
        public bool GyroEnabled { get => gyroEnabled; set => gyroEnabled = value; }
        #endregion

        private void Start()
        {
            if(PlayerPrefs.GetInt("isMuted") == 1)
            {
                isMuted = true;
            }
            else { isMuted = false; }
            if (PlayerPrefs.GetInt("tutorialEnabled") == 1)
            {
                tutorialEnabled = true;
            }
            else { tutorialEnabled = false; }
        }
        public void ToggleMusic()
        {
            isMuted = !isMuted;
            if (isMuted)
            {
                PlayerPrefs.SetInt("isMuted", 1);
            }
            else { PlayerPrefs.SetInt("isMuted", 0);  }
            
        }

        public void ToggleGyro()
        {
            gyroEnabled = !gyroEnabled;
            if (gyroEnabled)
            {
                PlayerPrefs.SetInt("gyroEnabled", 1);
            }
            else { PlayerPrefs.SetInt("gyroEnabled", 0); }

        }

    }
}



