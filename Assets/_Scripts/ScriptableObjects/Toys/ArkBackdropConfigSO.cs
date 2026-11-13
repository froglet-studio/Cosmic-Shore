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
    }

    /// <summary>Which of a room's clips: the room at rest, the conversion (played once), the room after it.</summary>
    public enum ArkBackdropRun
    {
        Loop = 0,
        Convert = 1,
        Done = 2,
    }

    /// <summary>
    /// The Ark screen's backdrops: the Frogma renders played as video behind each tab (Shombith, 2026-11-12: "do exactly
    /// how you showed me on Frogma, no need to use the actual model, it can be a video or gif of this high quality";
    /// Docs/MENU_REDESIGN_B.md section 190). SQUADS plays the hangar with the squad lined up (one clip per colour set and
    /// domain), CONVERTER the pier out in the cell with Garrett's combine (at rest, the conversion, the Omni made),
    /// ARTIFACTS the room of Rev 86; DECOR has none. A clip is chosen by room, run, the worn colour set (matched against
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
            [Tooltip("At rest (loops), the conversion (once) or after it (loops).")]
            public ArkBackdropRun run;
            [Tooltip("The colour set it was rendered in: the start of the set asset's name (Nocturne, Photic, DartFrog).")]
            public string colourSet;
            [Tooltip("The squad domain it was rendered in (the hangar's).")]
            public Domains domain;
            [Tooltip("On for a room the squad's domain does not change (the pier, the artifacts room).")]
            public bool anyDomain;
            public VideoClip clip;
        }

        /// <summary>
        /// Where the hangar clips' camera stood and where the pads are, so the game can stand each seat's REAL vessel on
        /// its pad over the video (Docs/MENU_REDESIGN_B.md section 202; Shombith, 2026-11-14). The hangar clips are
        /// rendered with their pads empty (Docs/MenuRedesignB/rounds/r110_squads_empty_pads.py); SquadVesselLayer draws the
        /// hulls from this camera, as a transparent layer over the picture. Units and places are the render's
        /// (r67_ark_tabs_render.py, Blender metres) with its z up turned into Unity's y up: (x, z, y).
        /// </summary>
        [Serializable]
        public struct HangarView
        {
            [Tooltip("Off: the hangar shows only its clip (empty pads).")]
            public bool enabled;
            [Tooltip("The clips' camera, at rest (r67 CAM[\"squads\"]).")]
            public Vector3 cameraPosition;
            [Tooltip("The point the camera looks at, at rest.")]
            public Vector3 cameraAim;
            [Tooltip("The camera's vertical field of view in degrees (the render's 24 mm lens on a 36 mm sensor at 16:9).")]
            public float verticalFov;
            [Tooltip("The camera's breathing over one loop: x across (once a loop) and y up (twice a loop), metres.")]
            public Vector2 cameraSway;
            [Tooltip("How far the aim swings across over one loop, metres.")]
            public float aimSway;
            [Tooltip("Seconds of one loop when no clip says (the clips are 3.2 s).")]
            public float loopSeconds;
            [Tooltip("The pads' centres on the floor, by seat: the player (the leader, a step back in the middle), then seats two and three.")]
            public Vector3[] pads;
            [Tooltip("Every hull is fitted to this length (its longest side), metres.")]
            public float hullLength;
            [Tooltip("Gap between the hull's lowest point and the pad, metres.")]
            public float clearance;
            [Tooltip("How far a live hull rises and sinks over one loop, metres.")]
            public float hover;
            [Tooltip("How far a live hull rocks about its nose over one loop, degrees.")]
            public float rockDegrees;
            [Tooltip("Each pad's hover starts this far along the loop after the one before (radians).")]
            public float phaseStep;
            [Tooltip("The pad's glow on a live hull, in the squad's domain: intensity and range (metres).")]
            public float padLightIntensity;
            public float padLightRange;
            [Tooltip("The bay's white lamp over every hull (an offline hull is lit by it alone): place above the player's pad, intensity, range.")]
            public Vector3 lampOffset;
            public float lampIntensity;
            public float lampRange;
            [Tooltip("Pixel size the vessels are drawn at (the clips' size); the layer is stretched over the picture with it.")]
            public Vector2Int layerSize;
            [Tooltip("Renders a second (the camera breathes, so it is drawn every frame up to this rate).")]
            public int renderRate;

            public static HangarView Default => new()
            {
                enabled = true,
                cameraPosition = new Vector3(6.0f, -15.6f, -85.0f),
                cameraAim = new Vector3(6.0f, -20.6f, -62.0f),
                verticalFov = 45.75f,
                cameraSway = new Vector2(0.18f, 0.08f),
                aimSway = 0.3f,
                loopSeconds = 3.2f,
                pads = new[] { new Vector3(0.0f, -23.0f, -66.0f), new Vector3(-9.0f, -23.0f, -60.5f), new Vector3(9.6f, -23.0f, -60.5f) },
                hullLength = 7.5f,
                clearance = 1.6f,
                hover = 0.12f,
                rockDegrees = 1.7f,
                phaseStep = 2.1f,
                padLightIntensity = 4.0f,
                padLightRange = 7.0f,
                lampOffset = new Vector3(-2.0f, 6.0f, -4.0f),
                lampIntensity = 2.0f,
                lampRange = 16.0f,
                layerSize = new Vector2Int(1920, 1080),
                renderRate = 30,
            };
        }

        [Tooltip("Every backdrop clip.")]
        [SerializeField] List<Clip> clips = new();
        [Tooltip("Pixel size of the texture the clips play into (they are 1920 x 1080).")]
        [SerializeField] Vector2Int textureSize = new(1920, 1080);

        [Header("The squad's vessels on the hangar's pads")]
        [Tooltip("The camera and pads the hangar clips were rendered with, for the live vessels drawn over them.")]
        [SerializeField] HangarView hangar = HangarView.Default;
        [Tooltip("Class -> prefab table the hulls are built from (Assets/_SO_Assets/Vessel Prefab Container.asset). Empty: no vessels.")]
        [SerializeField] VesselPrefabContainer vesselPrefabs;
        [Tooltip("What an OFFLINE mate's hull wears on its domain-role slots: the class livery (no team colour: powered down).")]
        [SerializeField] Material offlineLivery;

        public Vector2Int TextureSize => textureSize;
        public HangarView Hangar => hangar;
        public VesselPrefabContainer VesselPrefabs => vesselPrefabs;
        public Material OfflineLivery => offlineLivery;

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
