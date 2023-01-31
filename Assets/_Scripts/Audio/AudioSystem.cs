using System.Collections;
using TailGlider.Utility.Singleton;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Audio System to contain audio methods accessed by other classes
/// </summary>
namespace StarWriter.Core.Audio
{
    [DefaultExecutionOrder(-1)]
    public class AudioSystem : SingletonPersistent<AudioSystem>
    {
        #region Fields
        [SerializeField] AudioMixer masterMixer;
        [SerializeField] AudioSource musicSource1;
        [SerializeField] AudioSource musicSource2;
        [SerializeField] float masterVolume = .1f;
        [SerializeField] float musicVolume = .1f;

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        float MasterVolume { get { return isAudioEnabled ? masterVolume : 0; } set { } }
        float MusicVolume { get { return isAudioEnabled ? musicVolume : 0; } set { } }

        bool firstMusicSourceIsPlaying = true;
        bool isAudioEnabled = true;

        public bool IsAudioEnabled { get { return isAudioEnabled; } }
        #endregion

        void Start()
        {
            if (musicSource1 == null || musicSource2 == null)
            {
                Debug.LogError("Missing Music Source for Audio System. Disabling Audio.");
                isAudioEnabled = false;
            }
            else
            {
                isAudioEnabled = GameSetting.Instance.IsAudioEnabled;
            }

            Debug.Log($"Audio Enabled: {isAudioEnabled}");
            ChangeAudioEnabledStatus(isAudioEnabled);
        }

        void OnEnable()
        {
            GameSetting.OnChangeAudioEnabledStatus += ChangeAudioEnabledStatus;
        }

        void OnDisable()
        {
            GameSetting.OnChangeAudioEnabledStatus -= ChangeAudioEnabledStatus;
        }

        void ChangeAudioEnabledStatus(bool status)
        {
            Debug.Log($"AudioSystem.OnChangeAudioEnabledStatus - status: {status}");

            isAudioEnabled = status;            
            SetMasterMixerVolume(isAudioEnabled ? masterVolume : 0);
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
                activeAudioSource.volume = MasterVolume * (1 - (t / transitionTime));
                yield return null;
            }

            activeAudioSource.Stop();
            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            Debug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip masterVolume
                activeAudioSource.volume = MasterVolume * (t / transitionTime);
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
                originalSource.volume = MasterVolume * (1 - (t / transitionTime));
                newSource.volume = MasterVolume * (t / transitionTime);
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

        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            sfxSource.volume = MasterVolume;
            sfxSource.PlayOneShot(audioClip);
        }
        #region Mixer Methods
        public void SetMasterMixerVolume(float value)
        {
            masterMixer.SetFloat("MasterVolume", value);
        }

        public void SetMusicMixerVolume(float value)
        {
            masterMixer.SetFloat("MusicVolume", value);
        }

        public void SetEnvironmentMixerVolume(float value)
        {
            masterMixer.SetFloat("EnvironmentVolume", value);
        }
        #endregion
    }
}