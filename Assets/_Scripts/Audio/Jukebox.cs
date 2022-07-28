using System.Collections.Generic;
using TailGlider.Utility.Singleton;
using UnityEngine;

namespace StarWriter.Core.Audio
{
    public class Jukebox : SingletonPersistent<Jukebox>
    {
        [SerializeField] private SO_Song[] so_songs;  // Used for random and indexed song selection

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
            GameSetting.OnChangeAudioEnabledStatus += ChangeAudioEnabledStatus;
        }

        private void OnDisable()
        {
            GameSetting.OnChangeAudioEnabledStatus -= ChangeAudioEnabledStatus;
        }

        private void ChangeAudioEnabledStatus(bool status)
        {
            jukeboxIsOn = status;

            if (!jukeboxIsOn)
                CancelAllSongsPlaying();
        }

        private void Update()
        {
            if (!audioSystem.IsAudioEnabled)
                return;

            //if audio is enabled, the jukebox is on, and no music is playing, hit the side of the jukebox heeeeey
            if (jukeboxIsOn)
            {
                if (!audioSystem.IsMusicSourcePlaying())
                {
                    StartJukebox();
                }

                if (!audioSystem.MusicSource1.isPlaying && !audioSystem.MusicSource2.isPlaying)
                {
                    StartJukebox();
                }
            }
        }

        private void InitiatizeJukebox()  //Adds song SO's to the Playlist dictionary. Then initiatize the jukebox
        {
            foreach (SO_Song so in so_songs)
            {
                Song song = new Song(so);
                Playlist.Add(song.Title, song);
                Debug.Log("Song " + song.Title + " added to Playlist");
            }
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

        // TODO: Is this supposed to stop all songs playing until they're started again? If so, we need to disable to jukebox here as well
        // Alternatively, we can make this private and have all dis/enablement go through GameSetting.OnChangeAudioEnabledStatus
        public void CancelAllSongsPlaying()
        {
            audioSystem.MusicSource1.Stop();
            audioSystem.MusicSource2.Stop();
        }

        public void PlaySong(string title)
        {
            // TODO: This plays a song outside of the control flow of 'random' or 'cycle'. After this song plays, 'random' or 'cycle' will continue. Is this what we want?
            if (Playlist.TryGetValue(title, out Song song))
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

        // Start the song at the current index and increment the index to queue up the next song
        public void PlayNextSong()
        {
            audioSystem.PlayMusicClip(so_songs[index].Clip);
            index = (index + 1) % so_songs.Length;  // increment and modulo will cycle from 0 to length-1
        }
    }
}