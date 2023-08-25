using StarWriter.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        public delegate void OnChangeMusicEnabledStatusEvent(bool status);
        public static event OnChangeMusicEnabledStatusEvent OnChangeMusicEnabledStatus;

        public delegate void OnChangeSFXEnabledStatusEvent(bool status);
        public static event OnChangeSFXEnabledStatusEvent OnChangeSFXEnabledStatus;

        public delegate void OnChangeHapticsEnabledStatusEvent(bool status);
        public static event OnChangeHapticsEnabledStatusEvent OnChangeHapticsEnabledStatus;

        public delegate void OnChangeInvertYEnabledStatusEvent(bool status);
        public static event OnChangeInvertYEnabledStatusEvent OnChangeInvertYEnabledStatus;

        public enum PlayerPrefKeys
        {
            isInitialPlay  = 1,
            musicEnabled = 2,
            sfxEnabled = 3,
            hapticsEnabled = 4,
            invertYEnabled = 5,
            adsEnabled = 6,
            highScore = 7,
            score = 8,
        }

        #region Settings
        [SerializeField] bool musicEnabled = true;
        [SerializeField] bool sfxEnabled = true;
        [SerializeField] bool hapticsEnabled = true;
        [SerializeField] bool invertYEnabled = false;

        public bool MusicEnabled { get => musicEnabled; }
        public bool SFXEnabled { get => sfxEnabled; }
        public bool HapticsEnabled { get => hapticsEnabled; }
        public bool InvertYEnabled { get => invertYEnabled; }
        #endregion

        public override void Awake()
        {
            base.Awake();

            SetPlayerPrefDefault(PlayerPrefKeys.musicEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.sfxEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.hapticsEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.invertYEnabled, 0);

            PlayerPrefs.Save();

            musicEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.musicEnabled.ToString()) == 1;
            sfxEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.sfxEnabled.ToString()) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.hapticsEnabled.ToString()) == 1;
            invertYEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.invertYEnabled.ToString()) == 1;

            // Reset this everytime the player launches the game
            if (!PlayerPrefs.HasKey(PlayerPrefKeys.isInitialPlay.ToString()))
            {
                Debug.Log("First Try!");
            }
        }

        /// <summary>
        /// Toggles the Music on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeMusicEnabledSetting()
        {
            musicEnabled = !musicEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.musicEnabled.ToString(), musicEnabled ? 1 : 0);
            PlayerPrefs.Save();


            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);   //Event to toggle AudioSystem isAudioEnabled         
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeSFXEnabledSetting()
        {
            sfxEnabled = !sfxEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.sfxEnabled.ToString(), sfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);     
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeHapticsEnabledSetting()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.hapticsEnabled.ToString(), hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);      
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

        void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
        }
    }
}