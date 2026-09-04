using System;
using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;


namespace CosmicShore.Core
{
    /// <summary>
    /// The player's local settings: on/off toggles plus the three 0..1 level sliders. Persisted to
    /// PlayerPrefs immediately and mirrored to UGS Cloud Save (<see cref="PlayerSettingsCloudData"/>)
    /// so they roam across devices.
    ///
    /// <para><b>Precedence between the two stores is LAST-WRITER-WINS by timestamp.</b> Every local
    /// change stamps <see cref="PlayerPrefKeys.SettingsModifiedUtc"/> and copies the stamp into the
    /// cloud payload; when cloud data arrives it is applied only if its stamp is newer than the local
    /// one (<see cref="ShouldApplyCloud"/>). Before this rule the cloud snapshot was applied
    /// unconditionally, so a slider dragged to 0 and a quit within the 1.5 s save debounce - or any
    /// session where the save did not reach the cloud - came back on the next launch at whatever the
    /// cloud (or a fresh default of 1.0) still held. That was the "the slider does not save" report.</para>
    ///
    /// <para>The level keys are FLOATS. <c>PlayerPrefs.GetFloat</c> on a key that was written as an int
    /// returns the default (0), and the defaults used to be seeded with <c>SetInt</c> - so every fresh
    /// install booted with music and SFX at level 0 until the cloud stomped 1.0 back in. Legacy installs
    /// carrying the int-typed key are repaired once by <see cref="RepairLegacyLevel"/>.</para>
    /// </summary>
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

        // AnalyticsServiceFacade subscribes to these with constructor lambdas that can never be
        // removed — with domain reload disabled each Play press stacked another dead set.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticEvents()
        {
            OnChangeMusicEnabledStatus = null;
            OnChangeSFXEnabledStatus = null;
            OnChangeHapticsEnabledStatus = null;
            OnChangeInvertYEnabledStatus = null;
            OnChangeInvertThrottleEnabledStatus = null;
            OnChangeJoystickVisualsStatus = null;
            OnChangeMusicLevel = null;
            OnChangeSFXLevel = null;
            OnChangeHapticsLevel = null;
        }

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
            /// <summary>UTC ticks of the last LOCAL settings change, stored as a string (PlayerPrefs has no long).</summary>
            SettingsModifiedUtc = 15,
        }

        /// <summary>
        /// Two slider values closer than this are the same value: a slider drag reports the same float
        /// repeatedly and two UI bindings can forward one drag twice, neither of which should cost a
        /// PlayerPrefs write, a cloud save or an event broadcast.
        /// </summary>
        const float LevelEpsilon = 0.0005f;

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

        /// <summary>UTC ticks of the last local change (0 = never stamped by this build).</summary>
        long _localModifiedUtcTicks;

        /// <summary>
        /// PlayerPrefs.Save is a disk (or registry) write; a slider drag would otherwise issue one per
        /// frame. Changes set this flag and the flush happens once per frame in LateUpdate, plus on
        /// pause/quit so nothing is lost.
        /// </summary>
        bool _prefsDirty;

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;   // duplicate being destroyed - do not touch prefs or subscribe

            SetPlayerPrefDefault(PlayerPrefKeys.MusicEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.SFXEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.HapticsEnabled, 1);
            SetPlayerPrefDefault(PlayerPrefKeys.InvertYEnabled, 0);
            SetPlayerPrefDefault(PlayerPrefKeys.InvertThrottleEnabled, 0);
            SetPlayerPrefDefault(PlayerPrefKeys.JoystickVisualsEnabled, 1);

            // Levels are FLOAT keys. Seeding them with SetInt made GetFloat return 0 on every fresh
            // install (a type mismatch reads as the default) - silent music and SFX out of the box.
            SetPlayerPrefDefaultFloat(PlayerPrefKeys.MusicLevel, 1f);
            SetPlayerPrefDefaultFloat(PlayerPrefKeys.SFXLevel, 1f);
            SetPlayerPrefDefaultFloat(PlayerPrefKeys.HapticsLevel, 1f);

            _localModifiedUtcTicks = ReadLocalModifiedTicks();

            musicEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.MusicEnabled)) == 1;
            sfxEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.SFXEnabled)) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.HapticsEnabled)) == 1;
            invertYEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.InvertYEnabled)) == 1;
            invertThrottleEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled)) == 1;
            joystickVisualsEnabled = PlayerPrefs.GetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled)) == 1;
            musicLevel = RepairLegacyLevel(PlayerPrefKeys.MusicLevel);
            sfxLevel = RepairLegacyLevel(PlayerPrefKeys.SFXLevel);
            hapticsLevel = RepairLegacyLevel(PlayerPrefKeys.HapticsLevel);

            PlayerPrefs.Save();

            // Apply cloud-synced settings on top of local once cloud data is ready. The [Inject] field
            // is populated by the Bootstrap ContainerScope before this Awake on the normal boot path;
            // guard anyway so a scene that lacks a scope degrades to local-only instead of throwing.
            if (_ugsDataService == null)
            {
                CSDebug.LogWarning("[GameSetting] UGSDataService not injected - settings will not roam this session.");
            }
            else if (_ugsDataService.IsInitialized)
                ApplyCloudSettings(_ugsDataService.SettingsRepo);
            else
                _ugsDataService.OnInitialized += HandleCloudDataReady;
        }

        void OnDestroy()
        {
            if (_ugsDataService != null)
                _ugsDataService.OnInitialized -= HandleCloudDataReady;
        }

        void LateUpdate()
        {
            FlushPrefsIfDirty();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) FlushAll();
        }

        void OnApplicationQuit()
        {
            FlushAll();
        }

        /// <summary>
        /// Writes PlayerPrefs to disk and kicks the cloud repository's save. The repository writes
        /// its local snapshot synchronously before the network call, so even a quit that cannot wait
        /// for UGS leaves the last-known-good file on disk for the next (possibly offline) launch.
        /// </summary>
        void FlushAll()
        {
            FlushPrefsIfDirty();
            var repo = _ugsDataService?.SettingsRepo;
            if (repo != null && repo.IsDirty)
                _ = repo.SaveAsync();
        }

        void FlushPrefsIfDirty()
        {
            if (!_prefsDirty) return;
            _prefsDirty = false;
            PlayerPrefs.Save();
        }

        void HandleCloudDataReady()
        {
            _ugsDataService.OnInitialized -= HandleCloudDataReady;
            ApplyCloudSettings(_ugsDataService.SettingsRepo);
        }

        /// <summary>
        /// Pure precedence rule between the cloud snapshot and the local PlayerPrefs, so it can be
        /// unit-tested without a MonoBehaviour. Cloud wins only when it is strictly newer. A local
        /// store that has never been stamped (a fresh install, or the first run of this build) takes
        /// any cloud data that genuinely came from the cloud or its cache - that is the roaming case -
        /// but never a bare default that no one ever saved.
        /// </summary>
        public static bool ShouldApplyCloud(long cloudTicks, long localTicks, bool cloudHasPersistedData)
        {
            if (cloudTicks > localTicks) return true;
            return localTicks == 0 && cloudHasPersistedData;
        }

        /// <summary>
        /// Applies cloud-synced settings on top of local PlayerPrefs when the cloud copy is the newer
        /// one (see <see cref="ShouldApplyCloud"/>). When the LOCAL copy is newer the cloud is brought
        /// up to date instead, so the two stores converge either way.
        /// </summary>
        void ApplyCloudSettings(PlayerSettingsRepository repo)
        {
            var cloud = repo?.Data;
            if (cloud == null) return;

            if (!ShouldApplyCloud(cloud.ModifiedUtcTicks, _localModifiedUtcTicks, repo.HasPersistedData))
            {
                if (_localModifiedUtcTicks > cloud.ModifiedUtcTicks)
                {
                    CSDebug.Log("[GameSetting] Local settings are newer than the cloud snapshot - pushing local to cloud.");
                    SyncToCloud();
                }
                return;
            }

            musicEnabled = cloud.MusicEnabled;
            sfxEnabled = cloud.SFXEnabled;
            hapticsEnabled = cloud.HapticsEnabled;
            invertYEnabled = cloud.InvertYEnabled;
            invertThrottleEnabled = cloud.InvertThrottleEnabled;
            joystickVisualsEnabled = cloud.JoystickVisualsEnabled;
            musicLevel = Mathf.Clamp01(cloud.MusicLevel);
            sfxLevel = Mathf.Clamp01(cloud.SFXLevel);
            hapticsLevel = Mathf.Clamp01(cloud.HapticsLevel);
            _localModifiedUtcTicks = Math.Max(cloud.ModifiedUtcTicks, _localModifiedUtcTicks);

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
            WriteLocalModifiedTicks(_localModifiedUtcTicks);
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
            CommitLocalChange();
            OnChangeMusicEnabledStatus?.Invoke(musicEnabled);
        }

        public void ChangeSFXEnabledSetting()
        {
            sfxEnabled = !sfxEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.SFXEnabled), sfxEnabled ? 1 : 0);
            CommitLocalChange();
            OnChangeSFXEnabledStatus?.Invoke(sfxEnabled);
        }

        public void ChangeHapticsEnabledSetting()
        {
            hapticsEnabled = !hapticsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.HapticsEnabled), hapticsEnabled ? 1 : 0);
            CommitLocalChange();
            OnChangeHapticsEnabledStatus?.Invoke(hapticsEnabled);
        }

        public void ChangeInvertYEnabledStatus()
        {
            invertYEnabled = !invertYEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertYEnabled), invertYEnabled ? 1 : 0);
            CommitLocalChange();
            OnChangeInvertYEnabledStatus?.Invoke(invertYEnabled);
        }

        public void ChangeInvertThrottleEnabledStatus()
        {
            invertThrottleEnabled = !invertThrottleEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.InvertThrottleEnabled), invertThrottleEnabled ? 1 : 0);
            CommitLocalChange();
            OnChangeInvertThrottleEnabledStatus?.Invoke(invertThrottleEnabled);
        }

        public void ChangeJoystickVisualsStatus()
        {
            joystickVisualsEnabled = !joystickVisualsEnabled;
            PlayerPrefs.SetInt(nameof(PlayerPrefKeys.JoystickVisualsEnabled), joystickVisualsEnabled ? 1 : 0);
            CommitLocalChange();
            OnChangeJoystickVisualsStatus?.Invoke(joystickVisualsEnabled);
        }

        public void SetMusicLevel(float level)
        {
            level = Mathf.Clamp01(level);
            if (Mathf.Abs(level - musicLevel) < LevelEpsilon) return;
            musicLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.MusicLevel), level);
            CommitLocalChange();
            OnChangeMusicLevel?.Invoke(musicLevel);
        }

        public void SetSFXLevel(float level)
        {
            level = Mathf.Clamp01(level);
            if (Mathf.Abs(level - sfxLevel) < LevelEpsilon) return;
            sfxLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.SFXLevel), level);
            CommitLocalChange();
            OnChangeSFXLevel?.Invoke(sfxLevel);
        }

        public void SetHapticsLevel(float level)
        {
            level = Mathf.Clamp01(level);
            if (Mathf.Abs(level - hapticsLevel) < LevelEpsilon) return;
            hapticsLevel = level;
            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.HapticsLevel), level);
            CommitLocalChange();
            OnChangeHapticsLevel?.Invoke(hapticsLevel);
        }

        /// <summary>
        /// Every local mutation ends here: stamp the change time (which is what lets the cloud
        /// snapshot lose to it on the next launch), queue the PlayerPrefs flush, and mirror to cloud.
        /// </summary>
        void CommitLocalChange()
        {
            _localModifiedUtcTicks = DateTime.UtcNow.Ticks;
            WriteLocalModifiedTicks(_localModifiedUtcTicks);
            _prefsDirty = true;
            SyncToCloud();
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
            cloud.ModifiedUtcTicks = _localModifiedUtcTicks;

            ds.SettingsRepo.MarkDirty();
        }

        void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
        }

        void SetPlayerPrefDefaultFloat(PlayerPrefKeys key, float value)
        {
            if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetFloat(key.ToString(), value);
        }

        /// <summary>
        /// Reads a level key, repairing the legacy int-typed seed once. An install that predates this
        /// build has no modification stamp; on such an install a level that reads as exactly 0 is the
        /// type-mismatch default rather than a choice (the shipped slider UI could not persist a 0
        /// either, so no legacy user has one saved) and is reset to 1. Once stamped, 0 is honoured.
        /// </summary>
        float RepairLegacyLevel(PlayerPrefKeys key)
        {
            float v = PlayerPrefs.GetFloat(key.ToString(), 1f);
            if (_localModifiedUtcTicks == 0 && v <= 0f)
            {
                v = 1f;
                PlayerPrefs.SetFloat(key.ToString(), v);
            }
            return Mathf.Clamp01(v);
        }

        static long ReadLocalModifiedTicks()
        {
            var s = PlayerPrefs.GetString(nameof(PlayerPrefKeys.SettingsModifiedUtc), string.Empty);
            return long.TryParse(s, out long ticks) ? ticks : 0L;
        }

        static void WriteLocalModifiedTicks(long ticks)
        {
            PlayerPrefs.SetString(nameof(PlayerPrefKeys.SettingsModifiedUtc), ticks.ToString());
        }
    }
}
