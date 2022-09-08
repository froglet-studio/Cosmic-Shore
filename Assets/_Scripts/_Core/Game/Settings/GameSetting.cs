using TailGlider.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        public delegate void OnChangeGyroEnabledStatusEvent(bool status);
        public static event OnChangeGyroEnabledStatusEvent OnChangeGyroEnabledStatus;

        public delegate void OnChangeAudioMuteStatusEvent(bool status);
        public static event OnChangeAudioMuteStatusEvent OnChangeAudioEnabledStatus;

        public delegate void OnChangeInvertYEnabledStatusEvent(bool status);
        public static event OnChangeInvertYEnabledStatusEvent OnChangeInvertYEnabledStatus;

        public enum PlayerPrefKeys
        {
            isInitialPlay,
            isTutorialEnabled,
            isAudioEnabled,
            isGyroEnabled,
            invertYEnabled,
            adsEnabled,
            highScore,
            firstLifeHighScore,
            score,
        }

        #region Settings
        [SerializeField] bool tutorialHasBeenCompleted = false;
        [SerializeField] bool isAudioEnabled = true;
        [SerializeField] bool isGyroEnabled = true;
        [SerializeField] bool invertYEnabled = false;

        public bool TutorialHasBeenCompleted { get => tutorialHasBeenCompleted; set => tutorialHasBeenCompleted = value; }
        public bool IsAudioEnabled { get => isAudioEnabled; set => isAudioEnabled = value; }
        public bool IsGyroEnabled { get => isGyroEnabled; }
        public bool InvertYEnabled { get => invertYEnabled; }
        #endregion

        public override void Awake()
        {
            base.Awake();

            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isTutorialEnabled.ToString()))
                PlayerPrefs.SetInt(PlayerPrefKeys.isTutorialEnabled.ToString(), 1);
            
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isAudioEnabled.ToString()))
                PlayerPrefs.SetInt(PlayerPrefKeys.isAudioEnabled.ToString(), 1);
            
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.invertYEnabled.ToString()))
                PlayerPrefs.SetInt(PlayerPrefKeys.invertYEnabled.ToString(), 0);

            // We are turning off the Gyro functionality for the time being. Will be reintroduced as a ship upgrade.
            // if (!PlayerPrefs.HasKey(PlayerPrefKeys.isGyroEnabled.ToString()))
            PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), 0);

            PlayerPrefs.Save();

            isAudioEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isAudioEnabled.ToString()) == 1;
            isGyroEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isGyroEnabled.ToString()) == 1;
            invertYEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.invertYEnabled.ToString()) == 1;

            // Reset this everytime the player launches the game
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isInitialPlay.ToString()))
            {
                Debug.Log("First Try!");
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
            OnChangeAudioEnabledStatus?.Invoke(isAudioEnabled);   //Event to toggle AudioSystem isAudioEnabled         
        }

        /// <summary>
        /// Toggles the gyro on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeGyroEnabledStatus()
        {
            isGyroEnabled = !isGyroEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.isGyroEnabled.ToString(), isGyroEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeGyroEnabledStatus?.Invoke(isGyroEnabled);  // Event to toggle InputController isGryoEnabled
        }
        /// <summary>
        /// Toggles Invert Y Axis on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.invertYEnabled.ToString(), invertYEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);  // Event to toggle InputController isGryoEnabled
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