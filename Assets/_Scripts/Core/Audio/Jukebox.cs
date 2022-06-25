using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Jukebox : MonoBehaviour
{
    [SerializeField]
    private AudioClip[] allMusicAudioClips;

    Dictionary<string, AudioClip> Songs = new Dictionary<string, AudioClip>(); //string key to access a specific song

    public bool RandomizePlay = true;

    void Start()
    {
        InitiatizeJukebox();
        PlaySong();     
    }

    

    private void InitiatizeJukebox()
    {
        foreach (AudioClip song in allMusicAudioClips)
        {
            if (GetComponent<AudioClip>() != null)
            {
                AudioClip audioClip = GetComponent<AudioClip>();
                Songs.Add(audioClip.name, audioClip);     //TODO work on Key for ease of reference.  currently GO name
            }
        }
    }
    private void PlaySong()
    {
        if (RandomizePlay)
        {
            PlayRandomSong();
        }
    }

    public void PlayRandomSong()
    {
        AudioClip clip = allMusicAudioClips[UnityEngine.Random.RandomRange(0, allMusicAudioClips.Length)];
        GetComponent<AudioSource>().Play();
    }
}
