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
    /// The pilot SCENES the game ships (Docs/Pilots/PORTRAITS.md §8, "The scenes in the game"): per callsign, the
    /// widescreen picture of that callsign's painted pilot, the close-up cut from it (the avatar) and where that
    /// close-up sits in the picture.
    ///
    /// <para><b>A scene is named, never referenced.</b> <see cref="Entry.resource"/> is a path under a Resources
    /// folder, so loading this asset loads no scene: a page loads the one it shows and lets it go again
    /// (<c>PilotSceneImage</c>). The close-ups are referenced, because the crew list shows all of them at once and
    /// they are the avatars the profile already holds.</para>
    ///
    /// <para><b>Whose face it is.</b> A crew pilot is a person on a callsign (<see cref="PilotRecord"/>), and a
    /// callsign's art is its first painted pilot (PORTRAITS.md §6, decision 1), so the scene is found by callsign and
    /// <see cref="Entry.painted"/> names who is pictured.</para>
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
            [Tooltip("The callsign whose painted pilot this scene shows.")]
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

        [Tooltip("One row per callsign that has a painted scene, in callsign order.")]
        [SerializeField] List<Entry> scenes = new();

        public IReadOnlyList<Entry> Scenes => scenes;

        /// <summary>The scene of <paramref name="callsign"/>'s painted pilot. False when it has none.</summary>
        public bool TryGet(PilotVision callsign, out Entry entry)
        {
            entry = null;
            if (scenes == null) return false;
            foreach (var candidate in scenes)
            {
                if (candidate == null || candidate.callsign != callsign) continue;
                entry = candidate;
                return true;
            }
            return false;
        }
    }
}
