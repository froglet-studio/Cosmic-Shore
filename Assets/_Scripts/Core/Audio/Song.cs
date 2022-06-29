using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class Song : MonoBehaviour
{
    [SerializeField]
    SO_Song song_SO;

    string title;
    string description;
    string author;

    AudioClip clip;

    public string Title { get => title;  }
    public string Description { get => description;  }
    public string Author { get => author;  }
    public AudioClip Clip { get => clip;  }

    public void SetSongSO(SO_Song so)
    {
        song_SO = so;

        clip = song_SO.Clip;
        title = song_SO.Clip.name;
        description = song_SO.Decription;
        author = song_SO.Author;
    }

}
