using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Core
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

        public delegate void OnChangeInvertThrottleEnabledStatusEvent(bool status);
        public static event OnChangeInvertYEnabledStatusEvent OnChangeInvertThrottleEnabledStatus;

        public enum PlayerPrefKeys
        {
            IsInitialPlay = 1,
            MusicEnabled = 2,
            SFXEnabled = 3,
            HapticsEnabled = 4,
            InvertYEnabled = 5,
            AdsEnabled = 6,
            HighScore = 7,
            Score = 8,
            InvertThrottleEnabled = 9
        }

        #region Settings
        [SerializeField] bool musicEnabled = true;
        [SerializeField] bool sfxEnabled = true;
        [SerializeField] bool hapticsEnabled = true;
        [SerializeField] bool invertYEnabled = false;
        [SerializeField] bool invertThrottleEnabled = false;

        public bool MusicEnabled { get => musicEnabled; }
        public bool SFXEnabled { get => sfxEnabled; }
        public bool HapticsEnabled { get => hapticsEnabled; }
        public bool InvertYEnabled { get => invertYEnabled; }
        public bool InvertThrottleEnabled { get => invertThrottleEnabled; }
        #endregion

        public override void Awake()
        {
            base.Awake();

            SetPlayerPrefDefault(PlayerPrefKeys.MusicEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.SFXEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.HapticsEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.InvertYEnabled, 0);

            PlayerPrefs.Save();

            musicEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.MusicEnabled.ToString()) == 1;
            sfxEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.SFXEnabled.ToString()) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.HapticsEnabled.ToString()) == 1;
            invertYEnabled = PlayerPrefs.GetInt(PlayerPrefKeys.InvertYEnabled.ToString()) == 1;
        }

        /// <summary>
        /// Toggles the Music on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeMusicEnabledSetting()
        {
            musicEnabled = !musicEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.MusicEnabled.ToString(), musicEnabled ? 1 : 0);
            PlayerPrefs.Save();


            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);   //Event to toggle AudioSystem isAudioEnabled         
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeSFXEnabledSetting()
        {
            sfxEnabled = !sfxEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.SFXEnabled.ToString(), sfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);     
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeHapticsEnabledSetting()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.HapticsEnabled.ToString(), hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);      
        }

        /// <summary>
        /// Toggles Invert Y Axis on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.InvertYEnabled.ToString(), invertYEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);  // Event to toggle InputController isGryoEnabled
        }
        
        public void ChangeInvertThrottleEnabledStatus()
        {
            invertThrottleEnabled = !invertThrottleEnabled;
            PlayerPrefs.SetInt(PlayerPrefKeys.InvertThrottleEnabled.ToString(), invertThrottleEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeInvertThrottleEnabledStatus?.Invoke(invertThrottleEnabled);
        }

        void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
        }
    }
}