using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The game's soundtrack as the Audio Studio lists it (Docs/MENU_REDESIGN_B.md §56): one row per track, each with its
    /// own FMOD event. The events ship EMPTY (the FMOD rule: an unwired slot is a visible TODO): today the music is one
    /// event (<c>event:/Music/Music</c>) that plays all nine tracks at random, so a track cannot be picked until the FMOD
    /// project gives each track its own event. Until then the player keeps the old random music and the studio says so;
    /// the moment any row has an event, the playlist plays the ticked rows instead.
    /// </summary>
    [CreateAssetMenu(fileName = "Soundtrack", menuName = "ScriptableObjects/Audio/Soundtrack")]
    public class SoundtrackSO : ScriptableObject
    {
        [Serializable]
        public class Track
        {
            [Tooltip("Stable key the player's ticks are saved under. Never change it once shipped.")]
            public string id;
            [Tooltip("The name the Audio Studio and the now-playing card show.")]
            public string title;
            [Tooltip("The line under the name. Empty: the soundtrack's artist line.")]
            public string artist;
            [Tooltip("This track alone, not looping (the playlist moves on when it ends). Empty until the FMOD project " +
                     "splits the music event; never point it at a placeholder.")]
            public EventReference trackEvent;
            [Tooltip("In the rotation until the player unticks it.")]
            public bool onByDefault = true;
            [Tooltip("This track's audio file in the random music event (FMOD names each sound as it starts; case and " +
                     "extension are ignored). It is how the Audio Studio names the song while the random event plays.")]
            public string soundName;
        }

        [SerializeField] List<Track> tracks = new();
        [Tooltip("The line under a track's name when the track has none of its own.")]
        [SerializeField] string artistLine = "Cosmic Shore Original Soundtrack";

        public IReadOnlyList<Track> Tracks => tracks;
        public string ArtistLine => artistLine;

        /// <summary>At least one track has its own event, so tracks can be picked.</summary>
        public bool AnyTrackPlayable
        {
            get
            {
                foreach (var t in tracks)
                    if (t != null && !t.trackEvent.IsNull) return true;
                return false;
            }
        }
    }
}
