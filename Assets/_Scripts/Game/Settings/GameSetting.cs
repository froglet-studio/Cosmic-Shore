using CosmicShore.Utilities;
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
        public static event OnChangeInvertThrottleEnabledStatusEvent  OnChangeInvertThrottleEnabledStatus;

        public delegate void OnChangeJoystickVisualsStatusEvent(bool status);
        public static event OnChangeJoystickVisualsStatusEvent OnChangeJoystickVisualsStatus;

        public delegate void OnChangeMusicLevelEvent(float level);
        public static event OnChangeMusicLevelEvent OnChangeMusicLevel;

        public delegate void OnChangeSFXLevelEvent(float level);
        public static event OnChangeSFXLevelEvent OnChangeSFXLevel;

        public delegate void OnChangeHapticsLevelEvent(float level);
        public static event OnChangeHapticsLevelEvent OnChangeHapticsLevel;

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
            InvertThrottleEnabled = 9,
            JoystickVisualsEnabled = 10,
            MusicLevel = 11,
            SFXLevel = 12,
            HapticsLevel = 13,
            LastMissionDifficulty = 14,
    }

        #region Settings
        [SerializeField] bool musicEnabled = true;
        [SerializeField] bool sfxEnabled = true;
        [SerializeField] bool hapticsEnabled = true;
        [SerializeField] bool invertYEnabled = false;
        [SerializeField] bool invertThrottleEnabled = false;
        [SerializeField] bool joystickVisualsEnabled = true;
        [SerializeField] float musicLevel = 1.0f;
        [SerializeField] float sfxLevel = 1.0f;
        [SerializeField] float hapticsLevel = 1.0f;

        public bool MusicEnabled { get => musicEnabled; }
        public bool SFXEnabled { get => sfxEnabled; }
        public bool HapticsEnabled { get => hapticsEnabled; }
        public bool InvertYEnabled { get => invertYEnabled; }
        public bool InvertThrottleEnabled { get => invertThrottleEnabled; }
        public bool JoystickVisualsEnabled { get => joystickVisualsEnabled; }
        public float MusicLevel { get => musicLevel; }
        public float SFXLevel { get => sfxLevel; }
        public float HapticsLevel { get => hapticsLevel; }
        #endregion

        public override void Awake()
        {
            base.Awake();

            SetPlayerPrefDefault(PlayerPrefKeys.MusicEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.SFXEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.HapticsEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.InvertYEnabled, 0);
            SetPlayerPrefDefault(PlayerPrefKeys.InvertThrottleEnabled, 0);
            SetPlayerPrefDefault(PlayerPrefKeys.JoystickVisualsEnabled, 1);

            SetPlayerPrefDefault(PlayerPrefKeys.MusicLevel, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.SFXLevel, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.HapticsLevel, 1);

            PlayerPrefs.Save();

            musicEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.MusicEnabled)) == 1;
            sfxEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.SFXEnabled)) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.HapticsEnabled)) == 1;
            invertYEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.InvertYEnabled)) == 1;
            invertThrottleEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled)) == 1;
            joystickVisualsEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled)) == 1;
            musicLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.MusicLevel));
            sfxLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.SFXLevel));
            hapticsLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.HapticsLevel));
        }

        /// <summary>
        /// Toggles the Music on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeMusicEnabledSetting()
        {
            musicEnabled = !musicEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.MusicEnabled), musicEnabled ? 1 : 0);
            PlayerPrefs.Save();


            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);   //Event to toggle AudioSystem isAudioEnabled         
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeSFXEnabledSetting()
        {
            sfxEnabled = !sfxEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.SFXEnabled), sfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);     
        }

        /// <summary>
        /// Toggles the Mute on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeHapticsEnabledSetting()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.HapticsEnabled), hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);      
        }

        /// <summary>
        /// Toggles Invert Y Axis on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertYEnabled), invertYEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"Event has been fired, Inverted Y. Status : {invertYEnabled}");
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);  // Event to toggle InputController isGryoEnabled
        }
        
        public void ChangeInvertThrottleEnabledStatus()
        {
            invertThrottleEnabled = !invertThrottleEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled), invertThrottleEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"Event has been fired, Inverted Throttle. Status : {invertYEnabled}");
            OnChangeInvertThrottleEnabledStatus?.Invoke(invertThrottleEnabled);
        }

        public void ChangeJoystickVisualsStatus()
        {
            joystickVisualsEnabled = !joystickVisualsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled), joystickVisualsEnabled ? 1 :0);
            PlayerPrefs.Save();
            OnChangeJoystickVisualsStatus?.Invoke(joystickVisualsEnabled);
        }

        public void SetMusicLevel(float level)
        {
            musicLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.MusicLevel), level);
            PlayerPrefs.Save();
            OnChangeMusicLevel?.Invoke(musicLevel);
        }
        public void SetSFXLevel(float level)
        {
            sfxLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.SFXLevel), level);
            PlayerPrefs.Save();
            OnChangeSFXLevel?.Invoke(sfxLevel);
        }
        public void SetHapticsLevel(float level)
        {
            hapticsLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.HapticsLevel), level);
            PlayerPrefs.Save();
            OnChangeHapticsLevel?.Invoke(hapticsLevel);
        }

        void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
        }
    }
}