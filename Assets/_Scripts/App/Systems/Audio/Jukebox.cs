using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.App.Systems.Audio
{
    public class Jukebox : SingletonPersistent<Jukebox>
    {
        [SerializeField] SO_Song[] so_songs;  // Used for random and indexed song selection
        [SerializeField] SO_Song onDeathSong;

        AudioSystem audioSystem;
        Dictionary<string, Song> Playlist = new(); //use song title key to access a specific song

        int nextSongIndex = 0;
        bool jukeboxIsOn = true;

        public bool RandomizePlay = true;

        void OnEnable()
        {
            GameSetting.OnChangeMusicEnabledStatus += OnChangeAudioEnabledStatus;
        }

        void OnDisable()
        {
            GameSetting.OnChangeMusicEnabledStatus -= OnChangeAudioEnabledStatus;
        }

        void Start()
        {
            audioSystem = AudioSystem.Instance;
            InitializeJukebox();
            StartJukebox();     // Start the jukebox once initialization is complete
        }

        void Update()
        {
            if (!audioSystem.MusicEnabled)
                return;

            if (jukeboxIsOn && !audioSystem.IsMusicSourcePlaying())
                StartJukebox();
        }

        void OnDeathExplosionCompletion()
        {
            Song song = new Song(onDeathSong);
            PlaySong(song.Title);
        }

        void OnChangeAudioEnabledStatus(bool status)
        {
            jukeboxIsOn = status;

            if (jukeboxIsOn)
                PlayNextSong();
            else
                CancelAllSongsPlaying();
        }

        /// <summary>
        /// Adds songs to Playlist
        /// </summary>
        void InitializeJukebox()  // Adds song SO's to the Playlist dictionary. Then initialize the jukebox
        {
            // Songs added here are specific use songs
            if (onDeathSong != null)
            {
                Song song = new Song(onDeathSong);
                Playlist.Add(song.Title, song);
            }

            //Add songs from the so_songs list
            foreach (SO_Song so in so_songs)
            {
                Song song = new Song(so);
                Playlist.Add(song.Title, song);
                Debug.Log("Song " + song.Title + " added to Playlist");
            }
        }

        /// <summary>
        /// Sets up and starts Jukebox
        /// </summary>
        void StartJukebox()
        {
            if (RandomizePlay)
                PlayRandomSong();
            else
                PlayNextSong();
        }

        /// <summary>
        /// Play a specific song
        /// </summary>
        /// <param name="title">The title of the song - must already exist in the playlist</param>
        public void PlaySong(string title)
        {
            if (Playlist.TryGetValue(title, out Song song))
                audioSystem.PlayMusicClip(song.Clip);
        }

        /// <summary>
        /// Play a random song
        /// </summary>
        public void PlayRandomSong()
        {
            SO_Song so = so_songs[Random.Range(0, so_songs.Length)];
            AudioClip clip = so.Clip;
            audioSystem.PlayMusicClip(clip);
        }

        /// <summary>
        /// Play the next song in the playlist
        /// </summary>
        public void PlayNextSong()
        {
            // Start the song at the current index and increment the index to queue up the next song
            audioSystem.PlayMusicClip(so_songs[nextSongIndex].Clip);

            // Cycle Playlist Index - increment and modulo will cycle from 0 to length-1
            nextSongIndex = (nextSongIndex + 1) % so_songs.Length;
        }

        /// <summary>
        /// Stop playing any currently playing songs and turn the jukebox off
        /// </summary>
        public void CancelAllSongsPlaying()
        {
            audioSystem.StopAllSongs();
            jukeboxIsOn = false;
        }
    }
}