using UnityEngine;

namespace CosmicShore.App.Systems.Audio
{
    public class Song
    {
        readonly SO_Song song_SO;
        readonly AudioClip clip;
        readonly string title;
        readonly string description;
        readonly string author;

        public string Title { get => title; }
        public string Description { get => description; }
        public string Author { get => author; }
        public AudioClip Clip { get => clip; }

        public Song(SO_Song so)
        {
            song_SO = so;

            clip = song_SO.Clip;
            title = song_SO.Clip.name;
            description = song_SO.Decription;
            author = song_SO.Author;
        }
    }
}