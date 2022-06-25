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
    public class AudioSystem : SingletonPersistent<AudioSystem>
    {
        #region Fields
        [SerializeField]
        private AudioSource musicSource1; //Default Background Music      
        [SerializeField]
        private AudioSource musicSource2; //Default Background Music alt

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        [SerializeField]
        float volume = .1f;

        float Volume { get { return isAudioEnabled ? volume : 0; } set { } }

        private bool firstMusicSourceIsPlaying = true;
        private bool isAudioEnabled = true;
        private string AudioEnabledPlayerPrefKey = "isAudioEnabled";
        #endregion

        private void Start()
        {
            // Initialize volume
            isAudioEnabled = PlayerPrefs.GetInt(AudioEnabledPlayerPrefKey) == 1;
            ChangeAudioEnabledStatus(isAudioEnabled);   
        }

        private void OnEnable()
        {
            GameSetting.OnChangeAudioEnabledStatus += ChangeAudioEnabledStatus;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeAudioEnabledStatus -= ChangeAudioEnabledStatus;
        }

        private void ChangeAudioEnabledStatus(bool status)
        {
            isAudioEnabled = status;
            Debug.Log($"AudioSystem.Start - isAudioEnabled: {isAudioEnabled}");
            if (isAudioEnabled)
            {
                SetMasterAudioVolume(volume);
            }
            else
            {
                SetMasterAudioVolume(0);
            }
        }
        //public void PlayMusicClip(string audioSourcesKey)
        //{
        //    AudioSource activeAudioSource = AudioSources[audioSourcesKey];
        //    activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
        //    activeAudioSource.clip = AudioSources[audioSourcesKey].clip;
        //    activeAudioSource.volume = Volume;
        //    activeAudioSource.Play();
        //}
        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = Volume;
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
                activeAudioSource.volume = (Volume - t / transitionTime);
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
                originalSource.volume = (Volume - t / transitionTime);
                newSource.volume = Volume * (t / transitionTime);
                yield return null;
            }
            originalSource.Stop();
        }

        //public void PlaySFXClip(string audioSourcesKey, AudioSource sfxSource)
        //{
        //    AudioSource audioSource = sfxSource;
        //    AudioClip audioClip = AudioSources[audioSourcesKey].clip;
        //    Debug.Log("SFX Muton Playing");
        //    audioSource.PlayOneShot(audioClip, Volume);
        //    Debug.Log("SFX Muton Played");
        //}
        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            AudioSource audioSource = sfxSource;
            audioSource.PlayOneShot(audioClip);
        }

        // TODO: can we get eyes on this method? Should it be passed a value for volume, or just use the value from the local variable
        //public void PlaySFXClip(AudioClip audioClip, float volume)
        //{
        //    sfxSource.PlayOneShot(audioClip, volume);
        //}
        public void SetMasterAudioVolume(float volume)
        {
            SetMusicVolume(volume);
            //SetSFXVolume(volume);
        }
        public void SetMusicVolume(float volume)
        {
            musicSource1.volume = volume;
            musicSource2.volume = volume;
        }
        //public void SetSFXVolume(float volume)
        //{
        //    sfxSource.volume = volume;
        //}
    }
}

