using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility.Singleton;
using UnityEngine;
using UnityEngine.Audio;

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

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        float SFXVolume { get { return sfxEnabled ? sfxVolume : 0; } }
        float MusicVolume { get { return musicEnabled ? musicVolume : 0; } }

        bool firstMusicSourceIsPlaying = true;
        bool musicEnabled = true;
        bool sfxEnabled = true;

        Dictionary<MenuAudioCategory, AudioClip> MenuAudioClips;

        public bool MusicEnabled { get { return musicEnabled; } }
        public bool SFXEnabled { get { return sfxEnabled; } }
        #endregion

        void Start()
        {
            InitializeMenuAudioClips();

            // TODO: P1 - revisit necessity of this
            if (musicSource1 == null || musicSource2 == null)
            {
                Debug.LogError("Missing Music Source for Audio System. Disabling Audio.");
                musicEnabled = false;
            }
            else
            {
                musicEnabled = GameSetting.Instance.MusicEnabled;
            }
            sfxEnabled = GameSetting.Instance.SFXEnabled;
            ChangeMusicLevel(GameSetting.Instance.MusicLevel);
            ChangeSFXLevel(GameSetting.Instance.SFXLevel);

            ChangeMusicEnabledStatus(musicEnabled);
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
            Debug.Log($"AudioSystem.OnChangeAudioEnabledStatus - status: {status}");

            musicEnabled = status;            
            SetMixerMusicVolume(musicEnabled ? musicVolume : 0);
        }
        void ChangeSFXEnabledStatus(bool status)
        {
            sfxEnabled = status;
        }

        void ChangeMusicLevel(float level)
        {
            Debug.Log($"ChangeMusicLevel: {level}, {level/5f}");
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
            Debug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
        }

        public void PlayNextMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            Debug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
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
                // Fade out original clip masterVolume
                activeAudioSource.volume = SFXVolume * (1 - (t / transitionTime));
                yield return null;
            }

            activeAudioSource.Stop();
            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            Debug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip masterVolume
                activeAudioSource.volume = SFXVolume * (t / transitionTime);
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
            Debug.Log($"Playing New Music Clip: {newAudioSource.clip.name}");

            //crossfade music
            StartCoroutine(UpdateMusicWithCrossFade(activeAudioSource, newAudioSource, transitionTime));
        }

        IEnumerator UpdateMusicWithCrossFade(AudioSource originalSource, AudioSource newSource, float transitionTime)
        {
            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                originalSource.volume = SFXVolume * (1 - (t / transitionTime));
                newSource.volume = SFXVolume * (t / transitionTime);
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

        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }

        public void PlaySFXClip(AudioClip audioClip)
        {
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }
        #region Mixer Methods

        public void SetMixerMusicVolume(float value)
        {
            masterMixer.SetFloat("MusicVolume", value);
        }

        public void SetMixerSFXVolume(float value)
        {
            masterMixer.SetFloat("SFXVolume", value);
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
    }
}