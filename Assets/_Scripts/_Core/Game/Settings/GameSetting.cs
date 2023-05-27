using StarWriter.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        public delegate void OnChangeAudioMuteStatusEvent(bool status);
        public static event OnChangeAudioMuteStatusEvent OnChangeAudioEnabledStatus;

        public delegate void OnChangeInvertYEnabledStatusEvent(bool status);
        public static event OnChangeInvertYEnabledStatusEvent OnChangeInvertYEnabledStatus;

        public enum PlayerPrefKeys
        {
            isInitialPlay,
            isAudioEnabled,
            invertYEnabled,
            adsEnabled,
            highScore,
            firstLifeHighScore,
            score,
        }

        #region Settings
        [SerializeField] bool isAudioEnabled = true;
        [SerializeField] bool invertYEnabled = false;

        public bool IsAudioEnabled { get => isAudioEnabled; set => isAudioEnabled = value; }
        public bool InvertYEnabled { get => invertYEnabled; }
        #endregion

        public override void Awake()
        {
            base.Awake();
            
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isAudioEnabled.ToString()))
                PlayerPrefs.SetInt(PlayerPrefKeys.isAudioEnabled.ToString(), 1);
            
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.invertYEnabled.ToString()))
                PlayerPrefs.SetInt(PlayerPrefKeys.invertYEnabled.ToString(), 0);

            PlayerPrefs.Save();

            isAudioEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.isAudioEnabled.ToString()) == 1;
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
        /// Toggles Invert Y Axis on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.invertYEnabled.ToString(), invertYEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);  // Event to toggle InputController isGryoEnabled

            Debug.Log($"ChangeInvertYEnabledStatus: {invertYEnabled}");
        }
    }
}