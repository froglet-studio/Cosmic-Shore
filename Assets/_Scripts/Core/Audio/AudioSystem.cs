using System.Collections;
using System.Collections.Generic;
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
        [SerializeField]
        AudioMixer masterMixer;

        [SerializeField]
        private AudioSource musicSource1;     
        [SerializeField]
        private AudioSource musicSource2; 

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        [SerializeField]
        float masterVolume = .1f;
        [SerializeField]
        float musicVolume = .1f;
        [SerializeField]
        float environmentVolume = .1f;

        float MasterVolume { get { return isAudioEnabled ? masterVolume : 0; } set { } }

        private bool firstMusicSourceIsPlaying = true;
        private bool isAudioEnabled = true;
        private string AudioEnabledPlayerPrefKey = "isAudioEnabled";

        public bool IsAudioEnabled { get { return isAudioEnabled; } }
        #endregion

        private void Start()
        {
            // Initialize masterVolume
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
                SetMasterMixerVolume(masterVolume);
            }
            else
            {
                SetMasterMixerVolume(0);
            }
        }
  
        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            //activeAudioSource.volume = musicVolume;
            activeAudioSource.Play();
        }

        public void PlayNextMusicClip(AudioClip audioClip)
        {
            if (!IsMusicSourcePlaying()) 
            {
                AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
                activeAudioSource.clip = audioClip;
                //activeAudioSource.volume = musicVolume;
                activeAudioSource.Play();
            }
            else
            {
                if (musicSource1.isPlaying)
                {
                    WaitForMusicEnd(musicSource1);
                }
                else
                {
                    WaitForMusicEnd(musicSource2);
                }

            }
            
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
                // Fade out original clip masterVolume
                activeAudioSource.volume = (MasterVolume - t / transitionTime);
                yield return null;
            }
            activeAudioSource.Stop();

            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            for (t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip masterVolume
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
                originalSource.volume = (MasterVolume - t / transitionTime);
                newSource.volume = MasterVolume * (t / transitionTime);
                yield return null;
            }
            originalSource.Stop();
        }

        IEnumerator WaitForMusicEnd(AudioSource activeAudioSource)
        {
            while (activeAudioSource.isPlaying)
            {
                yield return null;
                IsMusicSourcePlaying();
            }
        }

        public bool IsMusicSourcePlaying()
        {
            if (musicSource1.isPlaying || musicSource2.isPlaying)
            {
                return true;
            }
            else { return false; }
        }

        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            AudioSource audioSource = sfxSource;
            audioSource.PlayOneShot(audioClip);
        }

        public void SetMasterMixerVolume(float value)
        {
            masterMixer.SetFloat("MasterVolume", value);
        }

        private float GetMasterMixerVolume()
        {
            masterMixer.GetFloat("MasterVolume", out masterVolume);
            return masterVolume;
        }

        public void SetMusicMixerVolume(float value)
        {
            masterMixer.SetFloat("MusicVolume", value);
        }

        private float GetMusicMixerVolume()
        {
            masterMixer.GetFloat("MusicVolume", out musicVolume);
            return masterVolume;
        }

        public void SetEnvironmentMixerVolume(float value)
        {
            masterMixer.SetFloat("EnvironmentVolume", value);
        }
       
        
    }
}

