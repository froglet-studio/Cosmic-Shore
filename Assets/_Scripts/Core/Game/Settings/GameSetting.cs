using System.Collections;
using System.Collections.Generic;
using Amoebius.Utility.Singleton;
using UnityEngine;


namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        public delegate void OnChangeGyroStatusEvent(bool status);
        public static event OnChangeGyroStatusEvent OnChangeGyroStatus;

        public delegate void OnChangeAudioMuteStatusEvent(bool status);
        public static event OnChangeAudioMuteStatusEvent OnChangeAudioMuteStatus;


        #region Settings
        [SerializeField]
        private bool isMuted = false;
        [SerializeField]
        private bool isTutorialEnabled = true;
        [SerializeField]
        private bool hasTutorialBeenCompleted = false;
        [SerializeField]
        private bool isGyroEnabled = true;

        public bool IsMuted { get => isMuted; set => isMuted = value; }
        public bool IsTutorialEnabled { get => isTutorialEnabled; set => isTutorialEnabled = value; }
        public bool HasTutorialBeenCompleted { get => hasTutorialBeenCompleted; set => hasTutorialBeenCompleted = value; }
        public bool IsGyroEnabled { get => isGyroEnabled; set => isGyroEnabled = value; }
        #endregion

        private void Start()
        {
            if(PlayerPrefs.GetInt("isMuted") == 1)
            {
                isMuted = true;
            }
            else { isMuted = false; }
            if (PlayerPrefs.GetInt("isTutorialEnabled") == 1)
            {
                isTutorialEnabled = true;
            }
            else { isTutorialEnabled = false; }
        }

        public void ChangeAudioMuteStatus()
        {
            isMuted = !isMuted;
            if (isMuted)
            {
                PlayerPrefs.SetInt("isMuted", 1);
            }
            else { PlayerPrefs.SetInt("isMuted", 0); }
            OnChangeAudioMuteStatus(isMuted);            
        }

        public void ChangeGyroStatus()
        {
            isGyroEnabled = !isGyroEnabled;
            if (isGyroEnabled)
            {
                PlayerPrefs.SetInt("isGyroEnabled", 1);
            }
            else { PlayerPrefs.SetInt("isGyroEnabled", 0); }
            OnChangeGyroStatus(isGyroEnabled);
        }

        public void TurnGyroOFF()
        {
            isGyroEnabled = false;
            PlayerPrefs.SetInt("isGyroEnabled", 0);
            OnChangeGyroStatus(isGyroEnabled);
        }

        public void TurnGyroON()
        {
            isGyroEnabled = true;
            PlayerPrefs.SetInt("isGyroEnabled", 1);
            OnChangeGyroStatus(isGyroEnabled);
        }
    }
}



