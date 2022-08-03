using System.Collections.Generic;
using TailGlider.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core.Audio
{
    public class Jukebox : SingletonPersistent<Jukebox>
    {
        [SerializeField] SO_Song[] so_songs;  // Used for random and indexed song selection

        [SerializeField]
        SO_Song onDeathSong;

        Dictionary<string, Song> Playlist = new Dictionary<string, Song>(); //use song title key to access a specific song

        private int index = 0;

        public bool RandomizePlay = true;

        private bool jukeboxIsOn = true;

        AudioSystem audioSystem;

        void Start()
        {
            audioSystem = AudioSystem.Instance;
            InitiatizeJukebox();
            StartJukebox();     // Start the jukebox once initiatization is complete
        }

        private void OnEnable()
        {
            GameSetting.OnChangeAudioEnabledStatus += OnChangeAudioEnabledStatus;
            AudioSystem.onMissingMusicSource += OnMissingAudioSystemSources;
            ShipExplosionHandler.onExplosionCompletion += OnDeathExplosionCompletion;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeAudioEnabledStatus -= OnChangeAudioEnabledStatus;
            AudioSystem.onMissingMusicSource -= OnMissingAudioSystemSources;
            ShipExplosionHandler.onExplosionCompletion += OnDeathExplosionCompletion;
        }
        /// <summary>
        /// Adds next 2 clips to the AudioSystem if a MusicSource is missing
        /// </summary>
        private void OnMissingAudioSystemSources()
        {
            IncrementSongListIndex();
            audioSystem.MusicSource1.clip = so_songs[index].Clip;
            IncrementSongListIndex();
            audioSystem.MusicSource2.clip = so_songs[index].Clip;
        }

        private void OnDeathExplosionCompletion()
        {
            Song song = new Song(onDeathSong);
            PlaySong(song.Title);
        }

        private void OnChangeAudioEnabledStatus(bool status)
        {
            jukeboxIsOn = status;

            if (jukeboxIsOn)
            {
                PlayNextSong();
            }
            else { CancelAllSongsPlaying(); }
        }

        private void Update()
        {
            if (!audioSystem.IsAudioEnabled)
                return;

            if (jukeboxIsOn)
            {
                if (audioSystem.IsMusicSourcePlaying())
                {
                    audioSystem.ConfirmMusicSourcesAreReady();   
                }
                else
                {
                    StartJukebox();
                }

  
            }
        }
        /// <summary>
        /// Adds songs to Playlist
        /// </summary>
        private void InitiatizeJukebox()  //Adds song SO's to the Playlist dictionary. Then initiatize the jukebox
        {
            // Songs added here are specific use songs
            Song song = new Song(onDeathSong);
            Playlist.Add(song.Title, song);

            //Add songs from the so_songs list
            foreach (SO_Song so in so_songs)
            {
                song = new Song(so);
                Playlist.Add(song.Title, song);
                Debug.Log("Song " + song.Title + " added to Playlist");
            }

        }
        /// <summary>
        /// Sets up and starts Jukebox
        /// </summary>
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

        // TODO: Is this supposed to stop all songs playing until they're started again? If so, we need to disable to jukebox here as well
        // Alternatively, we can make this private and have all dis/enablement go through GameSetting.OnChangeAudioEnabledStatus
        public void CancelAllSongsPlaying()
        {
            audioSystem.StopAllSongs();
            jukeboxIsOn = false;
        }
        /// <summary>
        /// Stop all other songs and plays a specific song
        /// </summary>
        /// <param name="title"></param>
        public void PlaySong(string title)
        {
            //audioSystem.StopAllSongs();

            // TODO: This plays a song outside of the control flow of 'random' or 'cycle'. After this song plays, 'random' or 'cycle' will continue. Is this what we want?
            if (Playlist.TryGetValue(title, out Song song))
            {
                audioSystem.PlayMusicClip(song.Clip);
            }
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

        // Start the song at the current index and increment the index to queue up the next song
        public void PlayNextSong()
        {
            audioSystem.PlayMusicClip(so_songs[index].Clip);
            IncrementSongListIndex();
        }

        private void IncrementSongListIndex()
        {
            index = (index + 1) % so_songs.Length;  // increment and modulo will cycle from 0 to length-
        }
    }
}