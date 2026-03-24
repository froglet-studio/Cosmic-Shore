using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;


namespace CosmicShore.Core
{
    public class GameSetting : SingletonPersistent<GameSetting>
    {
        [Inject] UGSDataService _ugsDataService;

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

            // Subscribe to cloud data ready event to apply cloud settings on top of local
            if (_ugsDataService.IsInitialized)
                ApplyCloudSettings(_ugsDataService.Settings?.Data);
            else
                _ugsDataService.OnInitialized += HandleCloudDataReady;
        }

        void OnDestroy()
        {
            if (_ugsDataService != null)
                _ugsDataService.OnInitialized -= HandleCloudDataReady;
        }

        void HandleCloudDataReady()
        {
            _ugsDataService.OnInitialized -= HandleCloudDataReady;
            ApplyCloudSettings(_ugsDataService.Settings?.Data);
        }

        /// <summary>
        /// Applies cloud-synced settings on top of local PlayerPrefs.
        /// Cloud data takes priority when available (enables roaming across devices).
        /// </summary>
        void ApplyCloudSettings(PlayerSettingsCloudData cloud)
        {
            if (cloud == null) return;

            musicEnabled = cloud.MusicEnabled;
            sfxEnabled = cloud.SFXEnabled;
            hapticsEnabled = cloud.HapticsEnabled;
            invertYEnabled = cloud.InvertYEnabled;
            invertThrottleEnabled = cloud.InvertThrottleEnabled;
            joystickVisualsEnabled = cloud.JoystickVisualsEnabled;
            musicLevel = cloud.MusicLevel;
            sfxLevel = cloud.SFXLevel;
            hapticsLevel = cloud.HapticsLevel;

            // Write cloud values back to PlayerPrefs for offline consistency
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.MusicEnabled), musicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.SFXEnabled), sfxEnabled ? 1 : 0);
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.HapticsEnabled), hapticsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertYEnabled), invertYEnabled ? 1 : 0);
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled), invertThrottleEnabled ? 1 : 0);
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled), joystickVisualsEnabled ? 1 : 0);
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.MusicLevel), musicLevel);
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.SFXLevel), sfxLevel);
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.HapticsLevel), hapticsLevel);
            PlayerPrefs.Save();

            // Fire all events so listeners pick up the new values
            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);
            OnChangeInvertThrottleEnabledStatus?.Invoke(invertThrottleEnabled);
            OnChangeJoystickVisualsStatus?.Invoke(joystickVisualsEnabled);
            OnChangeMusicLevel?.Invoke(musicLevel);
            OnChangeSFXLevel?.Invoke(sfxLevel);
            OnChangeHapticsLevel?.Invoke(hapticsLevel);

            CSDebug.Log("[GameSetting] Applied cloud settings.");
        }

        /// <summary>
        /// Toggles the Music on/off on options menu and pause menu panels
        /// </summary>
        public void ChangeMusicEnabledSetting()
        {
            musicEnabled = !musicEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.MusicEnabled), musicEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);
        }

        public void ChangeSFXEnabledSetting()
        {
            sfxEnabled = !sfxEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.SFXEnabled), sfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);
        }

        public void ChangeHapticsEnabledSetting()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.HapticsEnabled), hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);
        }

        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertYEnabled), invertYEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);
        }

        public void ChangeInvertThrottleEnabledStatus()
        {
            invertThrottleEnabled = !invertThrottleEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled), invertThrottleEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeInvertThrottleEnabledStatus?.Invoke(invertThrottleEnabled);
        }

        public void ChangeJoystickVisualsStatus()
        {
            joystickVisualsEnabled = !joystickVisualsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled), joystickVisualsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeJoystickVisualsStatus?.Invoke(joystickVisualsEnabled);
        }

        public void SetMusicLevel(float level)
        {
            musicLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.MusicLevel), level);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeMusicLevel?.Invoke(musicLevel);
        }

        public void SetSFXLevel(float level)
        {
            sfxLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.SFXLevel), level);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeSFXLevel?.Invoke(sfxLevel);
        }

        public void SetHapticsLevel(float level)
        {
            hapticsLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.HapticsLevel), level);
            PlayerPrefs.Save();
            SyncToCloud();
            OnChangeHapticsLevel?.Invoke(hapticsLevel);
        }

        /// <summary>
        /// Pushes current settings to UGS Cloud Save via PlayerSettingsRepository.
        /// Debounced by the repository's built-in save coalescing.
        /// </summary>
        void SyncToCloud()
        {
            var ds = _ugsDataService;
            if (ds?.SettingsRepo == null) return;

            var cloud = ds.SettingsRepo.Data;
            cloud.MusicEnabled = musicEnabled;
            cloud.SFXEnabled = sfxEnabled;
            cloud.HapticsEnabled = hapticsEnabled;
            cloud.InvertYEnabled = invertYEnabled;
            cloud.InvertThrottleEnabled = invertThrottleEnabled;
            cloud.JoystickVisualsEnabled = joystickVisualsEnabled;
            cloud.MusicLevel = musicLevel;
            cloud.SFXLevel = sfxLevel;
            cloud.HapticsLevel = hapticsLevel;

            ds.SettingsRepo.MarkDirty();
        }

        void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
        }
    }
}
