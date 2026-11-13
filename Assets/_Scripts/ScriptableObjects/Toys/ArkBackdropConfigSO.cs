using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>Which part of the Ark a tab looks at.</summary>
    public enum ArkRoom
    {
        None = 0,
        Hangar = 1,
        Pier = 2,
        Artifacts = 3,
        /// <summary>The hangar's workbench, where the crew PDA lies (PILOTS; Rev 99 C, section 191).</summary>
        Bench = 4,
    }

    /// <summary>Which of a room's clips: the room at rest, the conversion (played once), the room after it; for the
    /// bench, the PDA picked up (played once), held up to read (loops) and laid back down (played once).</summary>
    public enum ArkBackdropRun
    {
        Loop = 0,
        Convert = 1,
        Done = 2,
        PickUp = 3,
        Held = 4,
        PutDown = 5,
    }

    /// <summary>Which HyperSea pass carries the picture between rooms (the <c>_Style</c> of CosmicShore/UI/ArkPass).</summary>
    public enum ArkPassStyle
    {
        /// <summary>A: the pearl membrane crossing in the direction of travel.</summary>
        Membrane = 0,
        /// <summary>B: a ring spreading from the tab pressed, like a drop landing in the sea.</summary>
        Ripple = 1,
        /// <summary>E: the room reached found first along caustic light veins.</summary>
        CausticVeil = 2,
    }

    /// <summary>
    /// The Ark screen's backdrops: the Frogma renders played as video behind each tab (Shombith, 2026-11-12: "do exactly
    /// how you showed me on Frogma, no need to use the actual model, it can be a video or gif of this high quality";
    /// Docs/MENU_REDESIGN_B.md section 190). SQUADS plays the hangar with the squad lined up (one clip per colour set and
    /// domain), CONVERTER the pier out in the cell with Garrett's combine (at rest, the conversion, the Omni made),
    /// ARTIFACTS the room of Rev 86, PILOTS the hangar's workbench with the crew PDA lying on it (Rev 99 C: at rest, picked
    /// up, held, laid down; section 191); DECOR has none. A clip is chosen by room, run, the worn colour set (matched against
    /// the set asset's name) and the squad's domain, falling back to the first clip of the room and run.
    /// Written by <c>Tools/Build/author_ark_backdrop_assets.py</c>; the clips by
    /// <c>Docs/MenuRedesignB/rounds/r67_ark_backdrop_videos.py</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "ArkBackdropConfig", menuName = "ScriptableObjects/Toys/Ark Backdrops")]
    public sealed class ArkBackdropConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Clip
        {
            [Tooltip("The room this clip shows.")]
            public ArkRoom room;
            [Tooltip("At rest (loops), the conversion (once) or after it (loops); the bench's pick-up and put-down (once) and held (loops).")]
            public ArkBackdropRun run;
            [Tooltip("The colour set it was rendered in: the start of the set asset's name (Nocturne, Photic, DartFrog).")]
            public string colourSet;
            [Tooltip("The squad domain it was rendered in (the hangar's).")]
            public Domains domain;
            [Tooltip("On for a room the squad's domain does not change (the pier, the artifacts room).")]
            public bool anyDomain;
            public VideoClip clip;
        }

        [Tooltip("Every backdrop clip.")]
        [SerializeField] List<Clip> clips = new();
        [Tooltip("Pixel size of the texture the clips play into (they are 1920 x 1080).")]
        [SerializeField] Vector2Int textureSize = new(1920, 1080);

        [Header("Changes between rooms")]
        [Tooltip("The HyperSea pass (Docs/MENU_REDESIGN_B.md section 197): the shader that carries the picture from one " +
                 "room to the next through the Ark's pearl membrane (CosmicShore/UI/ArkPass). Empty: a change is a cut, " +
                 "at the next clip's first frame.")]
        [SerializeField] Shader passShader;
        [Tooltip("Seconds a pass from one room to another takes.")]
        [SerializeField, Min(0.05f)] float passSeconds = 0.5f;
        [Tooltip("The passes a change between rooms picks from at random (Shombith, 2026-11-13: \"randomise between A B " +
                 "and E every time for now until we finalise one\"). One entry is that pass every time; empty is the membrane.")]
        [SerializeField] List<ArkPassStyle> passStyles = new() { ArkPassStyle.Membrane, ArkPassStyle.Ripple, ArkPassStyle.CausticVeil };
        [Tooltip("Seconds the dissolve between one room's own clips takes (the pier: at rest, the conversion, the Omni made).")]
        [SerializeField, Min(0.05f)] float dissolveSeconds = 0.3f;

        public Vector2Int TextureSize => textureSize;
        public Shader PassShader => passShader;
        public float PassSeconds => passSeconds;
        public float DissolveSeconds => dissolveSeconds;
        public IReadOnlyList<ArkPassStyle> PassStyles => passStyles;

        /// <summary>The clip for <paramref name="room"/> and <paramref name="run"/> in the worn set and the squad's domain;
        /// the room and run's clip in another set or domain when there is no exact one; null when the room has none.</summary>
        public VideoClip Find(ArkRoom room, ArkBackdropRun run, string colourSetName, Domains domain)
        {
            VideoClip fallback = null, sameSet = null;
            foreach (var c in clips)
            {
                if (c.room != room || c.run != run || !c.clip) continue;
                bool set = !string.IsNullOrEmpty(colourSetName) && !string.IsNullOrEmpty(c.colourSet) &&
                           colourSetName.StartsWith(c.colourSet, StringComparison.OrdinalIgnoreCase);
                bool dom = c.anyDomain || c.domain == domain;
                if (set && dom) return c.clip;
                if (set && !sameSet) sameSet = c.clip;
                if (!fallback) fallback = c.clip;
            }
            return sameSet ? sameSet : fallback;
        }

        /// <summary>True when <paramref name="room"/> has any clip.</summary>
        public bool Has(ArkRoom room)
        {
            foreach (var c in clips)
                if (c.room == room && c.clip) return true;
            return false;
        }

        const string ResourcePath = "ArkBackdropConfig";

        /// <summary>The shipped config (Assets/Resources/ArkBackdropConfig.asset), for a view no one has wired. Null if absent.</summary>
        public static ArkBackdropConfigSO LoadDefault() => Resources.Load<ArkBackdropConfigSO>(ResourcePath);
    }
}
