using System;
using System.Collections;
using System.Collections.Generic;
using Amoebius.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core.Audio
{
    public class Jukebox : SingletonPersistent<Jukebox>
    {

        [SerializeField]
        private SO_Song[] so_songs;  //Used for random and indexed song selection

        Dictionary<string, Song> Playlist = new Dictionary<string, Song>(); //use song title key to access a specific song

        private int index = 0;

        public bool RandomizePlay = true;

        private bool jukeboxIsOn = true;

        AudioSystem audioSystem;

        void Start()
        {
            audioSystem = AudioSystem.Instance;
            InitiatizeJukebox();
            StartJukebox();
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
            if (status)
            {
                jukeboxIsOn = true;
            }
            if(!status)
            {
                CancelAllSongsPlaying();
                jukeboxIsOn = false;
            }
        }
        private void Update()
        {
            if (!jukeboxIsOn) { return; }  //Audio is off 

            if (audioSystem.CheckIfMusicSourceIsPlaying()) { return;  }  //currently a clip is playing

            if (!audioSystem.CheckIfMusicSourceIsPlaying() && PlayerPrefs.GetInt("isAudioEnabled") == 1) //No active clip and true
            {
                StartJukebox();
            }
        }

        private void InitiatizeJukebox()  //Adds song SO's to the Playlist dictionary
        {
            foreach (SO_Song so in so_songs)
            {
                Song song = new Song();
                song.SetSongSO(so);
                Playlist.Add(song.Title, song);
                Debug.Log("Song " + song.Title + " added to Playlist");
                index++;
            }
            index = 0;
        }
        private void StartJukebox()
        {
            if (RandomizePlay)
            {
                PlayRandomSong();
            }
            else
            {
                PlayNextSong();
            }

        }

        public void CancelAllSongsPlaying()
        {
            audioSystem.MusicSource1.Stop();
            audioSystem.MusicSource2.Stop();
        }

        public void PlaySong(string title)
        {
            Song song;
            if (Playlist.TryGetValue(title, out song))
            {
                audioSystem.PlayMusicClip(song.Clip);
            }
        }

        

        public void PlayRandomSong()
        {
            SO_Song so = so_songs[UnityEngine.Random.Range(0, so_songs.Length)];
            AudioClip clip = so.Clip;
            audioSystem.PlayMusicClip(clip);
        }

        public void PlayNextSong()
        {
            audioSystem.PlayMusicClip(so_songs[index].Clip);
            StartCoroutine(WaitForMusicToFinish());
        }

        IEnumerator WaitForMusicToFinish()
        {
            while (audioSystem.MusicSource1.isPlaying || audioSystem.MusicSource2.isPlaying)
            {
                yield return null;
            }
            index++;
            if (index > so_songs.Length - 1) //Reset to beginning of song list
            {
                index = 0;
            }
        }
    }
}

