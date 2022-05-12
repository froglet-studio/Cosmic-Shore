using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using StarWriter.Core;

/// <summary>
/// Simple Audio Manager by JVZ upgrade to AudioMaster if needed 
/// </summary>


namespace StarWriter.Core.Audio
{
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(GameSetting))]
    public class AudioManager : SingletonPersistant<AudioManager>
    {
        #region Fields
        [SerializeField]
        private AudioSource musicSource1;
        [SerializeField]
        private AudioSource musicSource2;
        [SerializeField]
        private AudioSource sfxSource;

        private bool firstMusicSourceIsPlaying;
        private bool isMuted = false;

        private GameSetting gameSetting;
        #endregion

        private void Start()
        {
            // Create AudioSources and save them as references
            musicSource1 = this.gameObject.AddComponent<AudioSource>();
            musicSource2 = this.gameObject.AddComponent<AudioSource>();
            sfxSource = this.gameObject.AddComponent<AudioSource>();
            isMuted = gameSetting.IsMuted;

            // Loop the music tracks
            musicSource1.loop = true;
            musicSource2.loop = true;
            PlayMusicClip(musicSource2.clip);
        }

        private void FixedUpdate()
        {
            if (isMuted)
            {
                SetMusicVolume(1f);
            }
            if (!isMuted)
            {
                SetMusicVolume(0f);
            }
        }

        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = 1;
            activeAudioSource.Play();
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

            float t = 0.0f;
            for (t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade out original clip volume
                activeAudioSource.volume = (1 - t / transitionTime);
                yield return null;
            }
            activeAudioSource.Stop();

            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            for (t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip volume
                activeAudioSource.volume = (t / transitionTime);
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
            
            //crossfade music
            StartCoroutine(UpdateMusicWithCrossFade(activeAudioSource, newAudioSource, transitionTime));
        }

        IEnumerator UpdateMusicWithCrossFade(AudioSource originalSource, AudioSource newSource, float transitionTime)
        {
            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                originalSource.volume = (1 - t / transitionTime);
                newSource.volume = (t / transitionTime);
                yield return null;
            }
            originalSource.Stop();
        }
        public void PlaySFXClip(AudioClip audioClip)
        {
            sfxSource.PlayOneShot(audioClip);
        }
        public void PlaySFXClip(AudioClip audioClip, float volume)
        {
            sfxSource.PlayOneShot(audioClip, volume);
        }
        public void SetMusicVolume(float volume)
        {
            musicSource1.volume = volume;
            musicSource2.volume = volume;
        }
        public void SetSFXVolume(float volume)
        {
            sfxSource.volume = volume;
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
        }
    }
}

