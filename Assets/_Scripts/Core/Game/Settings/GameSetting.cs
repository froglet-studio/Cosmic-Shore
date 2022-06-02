using Amoebius.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        public delegate void OnChangeGyroEnabledStatusEvent(bool status);
        public static event OnChangeGyroEnabledStatusEvent OnChangeGyroEnabledStatus;

        public delegate void OnChangeAudioMuteStatusEvent(bool status);
        public static event OnChangeAudioMuteStatusEvent OnChangeAudioEnabledStatus;

        public enum PlayerPrefKeys
        {
            isInitialPlay,
            isTutorialEnabled,
            isAudioEnabled,
            isGyroEnabled
        }

        #region Settings
        [SerializeField]
        private bool tutorialHasBeenCompleted = false;
        [SerializeField]
        private bool isAudioEnabled = true;
        [SerializeField]
        private bool isTutorialEnabled = true;
        [SerializeField]
        private bool isGyroEnabled = true;

        public bool TutorialHasBeenCompleted { get => tutorialHasBeenCompleted; set => tutorialHasBeenCompleted = value; }
        public bool IsAudioEnabled { get => isAudioEnabled; set => isAudioEnabled = value; }
        public bool IsTutorialEnabled { get => isTutorialEnabled; set => isTutorialEnabled = value; }
        public bool IsGyroEnabled { get => isGyroEnabled; set => isGyroEnabled = value; }
        #endregion

        public override void Awake()
        {
            base.Awake();
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isInitialPlay.ToString()))
            {
                //Initialize PlayerPrefs
                PlayerPrefs.SetInt(PlayerPrefKeys.isInitialPlay.ToString(), 0);  // set to false after first time
                PlayerPrefs.SetInt(PlayerPrefKeys.isTutorialEnabled.ToString(), 1);
                PlayerPrefs.SetInt(PlayerPrefKeys.isAudioEnabled.ToString(), 1);  // music always on first time playing
                PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), 1);
                PlayerPrefs.Save();

                //Initialize Bools
                isTutorialEnabled = true;
                isAudioEnabled = true;
                isGyroEnabled = true;
            }
            //Not the First Time Playing
            else
            {
                isAudioEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isAudioEnabled.ToString()) == 1;
                isTutorialEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isTutorialEnabled.ToString()) == 1;
                isGyroEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isGyroEnabled.ToString()) == 1;
            }
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeAudioEnabledStatus()
        {
            isAudioEnabled = !isAudioEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.isAudioEnabled.ToString(), isAudioEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeAudioEnabledStatus?.Invoke(isAudioEnabled);   //Event to toggle AudioManager isAudioEnabled         
        }

        /// <summary>
        /// Toggles the gyro on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeGyroEnabledStatus()
        {
            isGyroEnabled = !isGyroEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), isGyroEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"InputController.ChangeGyroEnabledStatus - isGyroEnabled: {isGyroEnabled}");
            OnChangeGyroEnabledStatus?.Invoke(isGyroEnabled);  //Event to toggle InputController isGryoEnabled
        }

        /// <summary>
        /// Tutorial Input Controller explicitly sets gyro off
        /// </summary>
        public void TurnGyroOFF()
        {
            isGyroEnabled = false;
            PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), 0);
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(false);
        }

        /// <summary>
        /// Tutorial Input Controller explicitly sets gyro on
        /// </summary>
        public void TurnGyroON()
        {
            isGyroEnabled = true;
            PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), 1);
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(true);
        }
    }
}



