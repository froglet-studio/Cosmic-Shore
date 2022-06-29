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

        AudioSystem audioSystem;

        void Start()
        {
            audioSystem = AudioSystem.Instance;
            InitiatizeJukebox();
            //PlaySong("I've waited so long"); Tested and works
            PlaySong("SEVERITY"); 
            //PlaySong();
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
        private void PlaySong()
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

        public void PlaySong(string title)
        {
            Song song;
            if (Playlist.TryGetValue(title, out song))
            {
                audioSystem.PlayMusicClip(song.Clip);
            }
        }

        public void PlayNextSong()
        {
            index++;
            audioSystem.MusicSource1.clip = so_songs[index].Clip;
            audioSystem.MusicSource2.clip = so_songs[index + 1].Clip;
            //audioSystem.PlayNextMusicClip();
           
            if (index > so_songs.Length - 1)
            {
                index = 0;
            }
        }

        public void PlayRandomSong()
        {
            SO_Song so = so_songs[UnityEngine.Random.Range(0, so_songs.Length - 1)];
            AudioClip clip = so.Clip;
            AudioSource audioSource = GetComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}

