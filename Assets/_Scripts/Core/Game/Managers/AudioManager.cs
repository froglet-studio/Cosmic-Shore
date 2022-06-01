using System;
using System.Collections;
using System.Collections.Generic;
using Amoebius.Utility.Singleton;
using UnityEngine;

// TODO: does not currently work
/// <summary>
/// Simple Audio Manager by JVZ upgrade to AudioMaster if needed 
/// </summary>
namespace StarWriter.Core.Audio
{
    [DefaultExecutionOrder(-1)]
    public class AudioManager : SingletonPersistent<AudioManager>
    {
        #region Fields
        [SerializeField]
        private AudioSource musicSource1;
        
        [SerializeField]
        private AudioSource musicSource2;

        Dictionary<string, AudioSource> AudioSources = new Dictionary<string, AudioSource>();

        public GameObject musicGO1;
        public GameObject musicGO2;
        public GameObject sfxGO3;

        [SerializeField]
        private AudioSource sfxSource;

        [SerializeField]
        float volume = 1f;

        private bool firstMusicSourceIsPlaying = true;
        private bool isMuted = false;
        #endregion

        private void Start()
        {
            // Create AudioSources and save them as references
            musicSource1 = musicGO1.GetComponent<AudioSource>();
            musicSource2 = musicGO2.GetComponent<AudioSource>();
            sfxSource = sfxGO3.GetComponent<AudioSource>();

            AudioSources.Add("Background Music 1", musicSource1);
            AudioSources.Add("Background Music 2", musicSource2);
            AudioSources.Add("Muton SFX 1", sfxSource);

            // Loop the music tracks
            musicSource1.loop = true;
            musicSource2.loop = true;

            PlayMusicClip(musicSource1.clip);    
        }

        private void OnEnable()
        {
            GameSetting.OnChangeAudioMuteStatus += ChangeMuteStatus;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeAudioMuteStatus -= ChangeMuteStatus;
        }

        private void ChangeMuteStatus(bool status)
        {
            isMuted = status;
            if (isMuted)
            {
                SetMasterAudioVolume(0);
            }
            else
            {
                SetMasterAudioVolume(volume);
            }
        }
        public void PlayMusicClip(string audioSourcesKey)
        {
            AudioSource activeAudioSource = AudioSources[audioSourcesKey];
            activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = AudioSources[audioSourcesKey].clip;
            activeAudioSource.volume = volume;
            activeAudioSource.Play();
        }
        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = volume;
            activeAudioSource.Play();
        }

        public AudioClip ToggleMusicPlaylist()
        {
            Debug.Log("Called play next song");
            firstMusicSourceIsPlaying = !firstMusicSourceIsPlaying;
            if (firstMusicSourceIsPlaying)
            {
                return musicSource2.clip;
            }
            else { return musicSource1.clip;  }
                
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

            float t;
            for (t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade out original clip volume
                activeAudioSource.volume = (volume - t / transitionTime);
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
                originalSource.volume = (volume - t / transitionTime);
                newSource.volume = volume*(t / transitionTime);
                yield return null;
            }
            originalSource.Stop();
        }

        public void PlaySFXClip(string audioSourcesKey)
        {
            AudioClip audioClip = AudioSources[audioSourcesKey].clip;
            Debug.Log("SFX Muton Playing");
            sfxSource.PlayOneShot(audioClip,volume);
            Debug.Log("SFX Muton Played");
        }
        public void PlaySFXClip(AudioClip audioClip)
        {
            sfxSource.PlayOneShot(audioClip);
        }
        public void PlaySFXClip(AudioClip audioClip, float volume)
        {
            sfxSource.PlayOneShot(audioClip, volume);
        }
        public void SetMasterAudioVolume(float volume)
        {
            musicSource1.volume = volume;
            musicSource2.volume = volume;
            sfxSource.volume = volume;
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

      
    }
}

