using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.Audio;
using CosmicShore.Utility;

/// <summary>
/// Audio System to contain audio methods accessed by other classes
/// </summary>
namespace CosmicShore.App.Systems.Audio
{
    [Serializable]
    public enum MenuAudioCategory
    {
        OptionClick = 1,
        OpenView = 2,
        SwitchView = 3,
        CloseView = 4,
        SmallReward = 5,
        BigReward = 6,
        Upgrade = 7,
        Denied = 8,
        Confirmed = 9,
        LetsGo = 10,
        SwitchScreen = 11,
        RedeemTicket = 12,
    }

    [Serializable]
    public enum GameplaySFXCategory
    {
        BlockDestroy = 1,
        ShieldActivate = 2,
        ShieldDeactivate = 3,
        MineExplode = 4,
        ProjectileLaunch = 5,
        CrystalCollect = 6,
        VesselImpact = 7,
        GameEnd = 8,
        ScoreReveal = 9,
        PauseOpen = 10,
        PauseClose = 11,
        GunFire = 12,
        BoostActivate = 13,
        Explosion = 14,
        CreatureDeath = 15,
        DriftStart = 16,
        DriftEnd = 17,
        EnergyGain = 18,
        SpeedBurst = 19,
    }

    [DefaultExecutionOrder(-1)]
    public class AudioSystem : SingletonPersistent<AudioSystem>
    {
        #region Fields
        [SerializeField] AudioMixer masterMixer;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioSource musicSource1;
        [SerializeField] AudioSource musicSource2;
        [SerializeField] float musicVolume = .1f;
        [SerializeField] float sfxVolume = .1f;

        [Header("Mixer Groups")]
        [SerializeField] AudioMixerGroup musicMixerGroup;
        [SerializeField] AudioMixerGroup sfxMixerGroup;

        [Header("SFX Throttling")]
        [SerializeField] float sfxCooldownSeconds = 0.05f;

        [Header("Spatial Audio")]
        [SerializeField] int spatialSourcePoolSize = 8;
        [SerializeField] float spatialMaxDistance = 500f;
        [SerializeField] float spatialMinDistance = 10f;

        [Header("Menu Audio")]
        [SerializeField] AudioClip OptionClickAudioClip;
        [SerializeField] AudioClip OpenViewAudioClip;
        [SerializeField] AudioClip SwitchViewAudioClip;
        [SerializeField] AudioClip CloseViewAudioClip;
        [SerializeField] AudioClip SmallRewardAudioClip;
        [SerializeField] AudioClip BigRewardAudioClip;
        [SerializeField] AudioClip UpgradeAudioClip;
        [SerializeField] AudioClip DeniedAudioClip;
        [SerializeField] AudioClip ConfirmedAudioClip;
        [SerializeField] AudioClip LetsGoAudioClip;
        [SerializeField] AudioClip SwitchScreenAudioClip;
        [SerializeField] AudioClip RedeemTicketAudioClip;

        [Header("Gameplay SFX")]
        [SerializeField] AudioClip BlockDestroyAudioClip;
        [SerializeField] AudioClip ShieldActivateAudioClip;
        [SerializeField] AudioClip ShieldDeactivateAudioClip;
        [SerializeField] AudioClip MineExplodeAudioClip;
        [SerializeField] AudioClip ProjectileLaunchAudioClip;
        [SerializeField] AudioClip CrystalCollectAudioClip;
        [SerializeField] AudioClip VesselImpactAudioClip;
        [SerializeField] AudioClip GameEndAudioClip;
        [SerializeField] AudioClip ScoreRevealAudioClip;
        [SerializeField] AudioClip PauseOpenAudioClip;
        [SerializeField] AudioClip PauseCloseAudioClip;
        [SerializeField] AudioClip GunFireAudioClip;
        [SerializeField] AudioClip BoostActivateAudioClip;
        [SerializeField] AudioClip ExplosionAudioClip;
        [SerializeField] AudioClip CreatureDeathAudioClip;
        [SerializeField] AudioClip DriftStartAudioClip;
        [SerializeField] AudioClip DriftEndAudioClip;
        [SerializeField] AudioClip EnergyGainAudioClip;
        [SerializeField] AudioClip SpeedBurstAudioClip;

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        float SFXVolume { get { return sfxEnabled ? sfxVolume : 0; } }
        float MusicVolume { get { return musicEnabled ? musicVolume : 0; } }

        bool firstMusicSourceIsPlaying = true;
        bool musicEnabled = true;
        bool sfxEnabled = true;

        Dictionary<MenuAudioCategory, AudioClip> MenuAudioClips;
        Dictionary<GameplaySFXCategory, AudioClip> GameplaySFXClips;

        readonly Dictionary<int, float> _lastPlayTime = new();
        AudioSource[] _spatialPool;
        int _spatialPoolIndex;

        public bool MusicEnabled { get { return musicEnabled; } }
        public bool SFXEnabled { get { return sfxEnabled; } }
        #endregion

        void Start()
        {
            InitializeMenuAudioClips();
            InitializeGameplaySFXClips();
            InitializeAudioSourcePriorities();
            InitializeMixerGroupRouting();
            InitializeSpatialPool();

            musicEnabled = GameSetting.Instance.MusicEnabled;
            sfxEnabled = GameSetting.Instance.SFXEnabled;
            ChangeMusicLevel(GameSetting.Instance.MusicLevel);
            ChangeSFXLevel(GameSetting.Instance.SFXLevel);
            ChangeMusicEnabledStatus(musicEnabled);
        }

        void InitializeAudioSourcePriorities()
        {
            musicSource1.priority = 0;
            musicSource2.priority = 0;
            sfxSource.priority = 128;
        }

        void InitializeMixerGroupRouting()
        {
            if (musicMixerGroup != null)
            {
                musicSource1.outputAudioMixerGroup = musicMixerGroup;
                musicSource2.outputAudioMixerGroup = musicMixerGroup;
            }
            if (sfxMixerGroup != null)
            {
                sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }

        void InitializeSpatialPool()
        {
            _spatialPool = new AudioSource[spatialSourcePoolSize];
            for (int i = 0; i < spatialSourcePoolSize; i++)
            {
                var go = new GameObject($"SpatialSFX_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = spatialMinDistance;
                src.maxDistance = spatialMaxDistance;
                src.priority = 200;
                src.playOnAwake = false;
                if (sfxMixerGroup != null)
                    src.outputAudioMixerGroup = sfxMixerGroup;
                _spatialPool[i] = src;
            }
        }

        void OnEnable()
        {
            GameSetting.OnChangeMusicEnabledStatus += ChangeMusicEnabledStatus;
            GameSetting.OnChangeSFXEnabledStatus += ChangeSFXEnabledStatus;
            GameSetting.OnChangeMusicLevel += ChangeMusicLevel;
            GameSetting.OnChangeSFXLevel += ChangeSFXLevel;
        }

        void OnDisable()
        {
            GameSetting.OnChangeMusicEnabledStatus -= ChangeMusicEnabledStatus;
            GameSetting.OnChangeSFXEnabledStatus -= ChangeSFXEnabledStatus;
        }

        void ChangeMusicEnabledStatus(bool status)
        {
            CSDebug.Log($"AudioSystem.OnChangeAudioEnabledStatus - status: {status}");

            musicEnabled = status;
            SetMixerMusicVolume(musicEnabled ? musicVolume : 0);
        }
        void ChangeSFXEnabledStatus(bool status)
        {
            sfxEnabled = status;
        }

        void ChangeMusicLevel(float level)
        {
            CSDebug.Log($"ChangeMusicLevel: {level}, {level/5f}");
            musicVolume = level / 5f;   // max .2 -- default max volume is too high
            musicSource1.volume = musicVolume;
            musicSource2.volume = musicVolume;
        }

        void ChangeSFXLevel(float level)
        {
            sfxVolume = level / 5f;   // max .2 -- default max volume is too high
        }

        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
        }

        public void PlayNextMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
        }

        public void PlayMusicClipWithFade(AudioClip audioClip, float transitionTime = 1.0f)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            StartCoroutine(UpdateMusicWithFade(activeAudioSource, audioClip, transitionTime));
        }

        IEnumerator UpdateMusicWithFade(AudioSource activeAudioSource, AudioClip newAudioClip, float transitionTime)
        {
            // Make sure source is active and playing
            if (!activeAudioSource.isPlaying)
                activeAudioSource.Play();

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade out original clip volume
                activeAudioSource.volume = MusicVolume * (1 - (t / transitionTime));
                yield return null;
            }

            activeAudioSource.Stop();
            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip volume
                activeAudioSource.volume = MusicVolume * (t / transitionTime);
                yield return null;
            }
        }

        public void PlayMusicClipWithCrossFade(AudioClip newAudioClip, float transitionTime = 1.0f)
        {
            //Determine the active audio source
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            AudioSource newAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);

            //Switch the bool
            firstMusicSourceIsPlaying = !firstMusicSourceIsPlaying;

            //Set the new audio source
            newAudioSource.clip = newAudioClip;
            newAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {newAudioSource.clip.name}");

            //crossfade music
            StartCoroutine(UpdateMusicWithCrossFade(activeAudioSource, newAudioSource, transitionTime));
        }

        IEnumerator UpdateMusicWithCrossFade(AudioSource originalSource, AudioSource newSource, float transitionTime)
        {
            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                originalSource.volume = MusicVolume * (1 - (t / transitionTime));
                newSource.volume = MusicVolume * (t / transitionTime);
                yield return null;
            }

            originalSource.Stop();
        }

        public void StopAllSongs()
        {
            musicSource1.Stop();
            musicSource2.Stop();
        }

        public bool IsMusicSourcePlaying()
        {
            return musicSource1.isPlaying || musicSource2.isPlaying;
        }

        public void PlayMenuAudio(MenuAudioCategory category)
        {
            PlaySFXClip(MenuAudioClips[category]);
        }

        public void PlayGameplaySFX(GameplaySFXCategory category)
        {
            if (GameplaySFXClips.TryGetValue(category, out var clip) && clip != null)
                PlaySFXClip(clip);
            else
                Debug.LogWarning($"AudioSystem.PlayGameplaySFX: No audio clip assigned for {category}");
        }

        public void PlayGameplaySFX(GameplaySFXCategory category, Vector3 worldPosition)
        {
            if (GameplaySFXClips.TryGetValue(category, out var clip) && clip != null)
                PlaySpatialSFX(clip, worldPosition);
            else
                Debug.LogWarning($"AudioSystem.PlayGameplaySFX: No audio clip assigned for {category}");
        }

        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }

        public void PlaySFXClip(AudioClip audioClip)
        {
            if (audioClip == null || !sfxEnabled) return;

            int clipId = audioClip.GetInstanceID();
            float now = Time.unscaledTime;

            if (_lastPlayTime.TryGetValue(clipId, out float lastTime)
                && now - lastTime < sfxCooldownSeconds)
                return;

            _lastPlayTime[clipId] = now;
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }

        void PlaySpatialSFX(AudioClip clip, Vector3 worldPosition)
        {
            if (!sfxEnabled || clip == null) return;

            int clipId = clip.GetInstanceID();
            float now = Time.unscaledTime;

            if (_lastPlayTime.TryGetValue(clipId, out float lastTime)
                && now - lastTime < sfxCooldownSeconds)
                return;

            _lastPlayTime[clipId] = now;

            var source = _spatialPool[_spatialPoolIndex];
            _spatialPoolIndex = (_spatialPoolIndex + 1) % spatialSourcePoolSize;

            source.transform.position = worldPosition;
            source.volume = SFXVolume;
            source.PlayOneShot(clip);
        }

        #region Mixer Methods

        public void SetMixerMusicVolume(float value)
        {
            masterMixer.SetFloat("MusicVolume", value);
        }

        public void SetMixerSFXVolume(float value)
        {
            // The SFX group's exposed parameter in Main_AudioMixer is "EnvironmentVolume"
            masterMixer.SetFloat("EnvironmentVolume", value);
        }
        #endregion

        void InitializeMenuAudioClips()
        {
            MenuAudioClips = new Dictionary<MenuAudioCategory, AudioClip>()
            {
                {MenuAudioCategory.OptionClick, OptionClickAudioClip},
                {MenuAudioCategory.OpenView, OpenViewAudioClip},
                {MenuAudioCategory.SwitchView, SwitchViewAudioClip},
                {MenuAudioCategory.CloseView, CloseViewAudioClip},
                {MenuAudioCategory.SmallReward, SmallRewardAudioClip},
                {MenuAudioCategory.BigReward, BigRewardAudioClip},
                {MenuAudioCategory.Upgrade, UpgradeAudioClip},
                {MenuAudioCategory.Denied, DeniedAudioClip},
                {MenuAudioCategory.Confirmed, ConfirmedAudioClip},
                {MenuAudioCategory.LetsGo, LetsGoAudioClip},
                {MenuAudioCategory.SwitchScreen, SwitchScreenAudioClip},
                {MenuAudioCategory.RedeemTicket, RedeemTicketAudioClip},
            };
        }

        void InitializeGameplaySFXClips()
        {
            GameplaySFXClips = new Dictionary<GameplaySFXCategory, AudioClip>()
            {
                {GameplaySFXCategory.BlockDestroy, BlockDestroyAudioClip},
                {GameplaySFXCategory.ShieldActivate, ShieldActivateAudioClip},
                {GameplaySFXCategory.ShieldDeactivate, ShieldDeactivateAudioClip},
                {GameplaySFXCategory.MineExplode, MineExplodeAudioClip},
                {GameplaySFXCategory.ProjectileLaunch, ProjectileLaunchAudioClip},
                {GameplaySFXCategory.CrystalCollect, CrystalCollectAudioClip},
                {GameplaySFXCategory.VesselImpact, VesselImpactAudioClip},
                {GameplaySFXCategory.GameEnd, GameEndAudioClip},
                {GameplaySFXCategory.ScoreReveal, ScoreRevealAudioClip},
                {GameplaySFXCategory.PauseOpen, PauseOpenAudioClip},
                {GameplaySFXCategory.PauseClose, PauseCloseAudioClip},
                {GameplaySFXCategory.GunFire, GunFireAudioClip},
                {GameplaySFXCategory.BoostActivate, BoostActivateAudioClip},
                {GameplaySFXCategory.Explosion, ExplosionAudioClip},
                {GameplaySFXCategory.CreatureDeath, CreatureDeathAudioClip},
                {GameplaySFXCategory.DriftStart, DriftStartAudioClip},
                {GameplaySFXCategory.DriftEnd, DriftEndAudioClip},
                {GameplaySFXCategory.EnergyGain, EnergyGainAudioClip},
                {GameplaySFXCategory.SpeedBurst, SpeedBurstAudioClip},
            };
        }
    }
}