using System.Collections;
using System.Collections.Generic;
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


        #region Settings
        [SerializeField]
        private bool isAudioEnabled = true;
        [SerializeField]
        private bool isTutorialEnabled = true;
        [SerializeField]
        private bool tutorialHasBeenCompleted = false;
        [SerializeField]
        private bool isGyroEnabled = true;

        public bool IsAudioEnabled { get => isAudioEnabled; set => isAudioEnabled = value; }
        public bool IsTutorialEnabled { get => isTutorialEnabled; set => isTutorialEnabled = value; }
        public bool TutorialHasBeenCompleted { get => tutorialHasBeenCompleted; set => tutorialHasBeenCompleted = value; }
        public bool IsGyroEnabled { get => isGyroEnabled; set => isGyroEnabled = value; }
        #endregion

        public override void Awake()
        {
            base.Awake();
            //First Time Playing
            if (!PlayerPrefs.HasKey("isInitialPlay"))
            {
                //Initialize PlayerPrefs
                PlayerPrefs.SetInt("isInitialPlay", 0);  //set to false after first time
                PlayerPrefs.SetInt("isTutorialEnabled", 1);
                PlayerPrefs.SetInt("isAudioEnabled", 1);  // music always on first time playing
                PlayerPrefs.SetInt("isGyroEnabled", 1);
                PlayerPrefs.Save();

                //Initialize Bools
                isTutorialEnabled = true;
                isAudioEnabled = true;
                isGyroEnabled = true;
            }
            //Not the First Time Playing
            else
            {
                isAudioEnabled = PlayerPrefs.GetInt("isAudioEnabled") == 1;
                isTutorialEnabled = PlayerPrefs.GetInt("isTutorialEnabled") == 1;
                isGyroEnabled = PlayerPrefs.GetInt("isGyroEnabled") == 1;
            }
        }
        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeAudioEnabledStatus()
        {
            isAudioEnabled = !isAudioEnabled;
            if (isAudioEnabled)
            {
                PlayerPrefs.SetInt("isAudioEnabled", 1);
            }
            else { PlayerPrefs.SetInt("isAudioEnabled", 0); }
            PlayerPrefs.Save();
            OnChangeAudioEnabledStatus?.Invoke(isAudioEnabled);   //Event to toggle AudioManager isAudioEnabled         
        }
        /// <summary>
        /// Toggles the gyro on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeGyroEnabledStatus()
        {
            isGyroEnabled = !isGyroEnabled;
            if (isGyroEnabled)
            {
                PlayerPrefs.SetInt("isGyroEnabled", 1);
            }
            else { PlayerPrefs.SetInt("isGyroEnabled", 0); }
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(isGyroEnabled);  //Event to toggle InputController isGryoEnabled
        }
        /// <summary>
        /// Tutorial Input Controller explicitly sets gyro off
        /// </summary>
        public void TurnGyroOFF()
        {
            isGyroEnabled = false;
            PlayerPrefs.SetInt("isGyroEnabled", 0);
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(false);
        }
        /// <summary>
        /// Tutorial Input Controller explicitly sets gyro on
        /// </summary>
        public void TurnGyroON()
        {
            isGyroEnabled = true;
            PlayerPrefs.SetInt("isGyroEnabled", 1);
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(true);
        }
    }
}



