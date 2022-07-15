using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StarWriter.Core.Audio
{
    public class Song
    {
        SO_Song song_SO;

        readonly string title;
        readonly string description;
        readonly string author;
        readonly AudioClip clip;

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

