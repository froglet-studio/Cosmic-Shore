using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>The side of its scene a pilot stands on. A page puts its words on the other side.</summary>
    public enum PilotSceneSide
    {
        Left = 0,
        Centre = 1,
        Right = 2,
    }

    /// <summary>
    /// The pilot SCENES the game ships (Docs/Pilots/PORTRAITS.md §8, "The scenes in the game"): per painted pilot (all
    /// fifty, Garrett 2026-11-16), the widescreen picture, the close-up cut from it (the avatar) and where that close-up
    /// sits in the picture.
    ///
    /// <para><b>A scene is named, never referenced.</b> <see cref="Entry.resource"/> is a path under a Resources
    /// folder, so loading this asset loads no scene: a page loads the one it shows and lets it go again
    /// (<c>PilotSceneImage</c>). The close-ups are referenced, because the crew list shows all of them at once and
    /// they are the avatars the profile already holds.</para>
    ///
    /// <para><b>Whose face it is.</b> A painted pilot's record names its scene (<see cref="PilotRecord.PaintedKey"/>,
    /// <see cref="TryGetFor"/>). A rolled person has no picture of their own and shows their callsign's own painted pilot
    /// (the one whose key is the callsign's word), and <see cref="Entry.painted"/> names who is pictured.</para>
    ///
    /// <para>Written by <c>Tools/Build/author_pilot_scene_assets.py</c> from <c>Docs/Pilots/art/scenes/scenes.json</c>;
    /// its <c>--check</c> fails on a hand edit. <see cref="TryGet"/> is a quiet probe: a callsign with no scene is an
    /// expected state (its page shows the words on the plain ground), not a fault.</para>
    /// </summary>
    public class PilotSceneCatalogSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("The painted pilot's word: the scene's name (VOLT, Astrofoil), as PilotRecord.PaintedKey holds it.")]
            public string key;

            [Tooltip("The callsign the painted pilot carries. More than one painted pilot may carry a callsign.")]
            public PilotVision callsign;

            [Tooltip("The painted pilot's name, for a page's caption. Empty leaves the caption without a name.")]
            public string painted;

            [Tooltip("The scene under a Resources folder, without its extension. Loaded only while a page shows it.")]
            public string resource;

            [Tooltip("The scene's close-up: the avatar cut from it.")]
            public Sprite closeUp;

            [Tooltip("Where the close-up sits in the scene, 0..1 from the bottom left. A page's crop keeps it in view.")]
            public Rect face;

            [Tooltip("The side the pilot stands on. A page puts its words on the other side.")]
            public PilotSceneSide side;
        }

        [Tooltip("One row per painted pilot, in the order a crew is granted them.")]
        [SerializeField] List<Entry> scenes = new();

        public IReadOnlyList<Entry> Scenes => scenes;

        /// <summary>The scene of the painted pilot <paramref name="key"/>. False when there is none.</summary>
        public bool TryGet(string key, out Entry entry)
        {
            entry = null;
            if (scenes == null || string.IsNullOrEmpty(key)) return false;
            foreach (var candidate in scenes)
            {
                if (candidate == null || candidate.key != key) continue;
                entry = candidate;
                return true;
            }
            return false;
        }

        /// <summary>The scene of <paramref name="callsign"/>'s own painted pilot (the one whose key is its word).</summary>
        public bool TryGet(PilotVision callsign, out Entry entry) => TryGet(Callsigns.WordOf(callsign), out entry);

        /// <summary>The scene a crew person shows: their own when they are a painted pilot, else their callsign's own
        /// painted pilot. A quiet probe: false when neither has one.</summary>
        public bool TryGetFor(PilotRecord person, out Entry entry)
        {
            entry = null;
            if (person == null) return false;
            return TryGet(person.PaintedKey, out entry) || TryGet(person.Vision(), out entry);
        }
    }
}
