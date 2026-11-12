using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Ark screen's 3D: the menu is INSIDE the Ark (Shombith, 2026-11-06/12; Docs/MENU_REDESIGN_B.md sections 166-168).
    /// One off-screen stage holds the Ark's own model (<see cref="arkModel"/>, its clam doors swung clear), and each Ark tab
    /// looks at its own part of it through one camera:
    /// <list type="bullet">
    /// <item>SQUADS - the hangar, from the back toward the pads (Rev 61 C's camera, section 136);</item>
    /// <item>CONVERTER - from inside the hangar, out of the open mouth, down the PIER that carries the hangar's floor out into
    /// the cell, Garrett's omni combine on its round deck (Rev 67's converter, section 168);</item>
    /// <item>ARTIFACTS - the artifacts room: the Reliquary's platform (the found artifacts on pedestals round a column of light)
    /// in the Gallery's hall (alcoves for the ones still to be found, a porthole at the end) (Rev 86, section 167);</item>
    /// <item>DECOR - none: locked, no room yet.</item>
    /// </list>
    /// Positions are in the Ark model's own space (Unity model space: x right, y up, z toward the nose; units as built,
    /// <see cref="ArkModelDefinitionSO.Scale"/> 1 = 196 from nose to jets). The hangar spans x +-14, y -23 (floor) to -12, z 44
    /// (back wall) to the doors at about 86.4. Read by <c>ArkInteriorStage</c>; written by
    /// <c>Tools/Build/author_ark_interior_assets.py</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "ArkInteriorConfig", menuName = "ScriptableObjects/Toys/Ark Interior")]
    public sealed class ArkInteriorConfigSO : ScriptableObject
    {
        /// <summary>One tab's camera, in the Ark model's space.</summary>
        [Serializable]
        public struct View
        {
            [Tooltip("Camera position, model space.")]
            public Vector3 position;
            [Tooltip("The point it looks at, model space.")]
            public Vector3 lookAt;
            [Tooltip("Vertical field of view, degrees.")]
            [Range(10f, 90f)] public float verticalFov;
            [Tooltip("Degrees per second of a slow side-to-side drift (0 = still). Keep it 0 where something is laid over the picture.")]
            [Min(0f)] public float driftDegrees;
        }

        [Header("The Ark")]
        [Tooltip("The Ark's model (ArkReverseModel.asset): its FBX, rig table, domain materials and the slot brightnesses the menu Ark is dressed with.")]
        [SerializeField] ArkModelDefinitionSO arkModel;
        [Tooltip("The box mesh every plain and danger prism is drawn with (Assets/_Models/Testing/Prism.asset, BlueBlock.prefab's mesh).")]
        [SerializeField] Mesh prismBoxMesh;
        [Tooltip("Degrees the top clam door swings about the model's x axis from rest. The model's own open take is -75; -150 folds it clear of the mouth so the pier reads.")]
        [SerializeField, Range(-180f, 180f)] float topDoorDegrees = -150f;
        [Tooltip("Degrees the bottom clam door swings about the model's x axis from rest (the open take is +70).")]
        [SerializeField, Range(-180f, 180f)] float bottomDoorDegrees = 150f;

        [Header("Cameras")]
        [SerializeField] View hangarView = new()
            { position = new Vector3(-6f, -15.6f, 85f), lookAt = new Vector3(-6f, -20.6f, 62f), verticalFov = 45.6f, driftDegrees = 0.4f };
        [SerializeField] View pierView = new()
            { position = new Vector3(0f, -16.4f, 63f), lookAt = new Vector3(0f, -19.6f, 128f), verticalFov = 45.6f, driftDegrees = 0f };
        [SerializeField] View artifactsView = new()
            { position = new Vector3(-3.6f, -16.4f, 590.5f), lookAt = new Vector3(-0.6f, -16.6f, 606f), verticalFov = 53.7f, driftDegrees = 0.3f };
        [Tooltip("Near and far clip of the interior camera.")]
        [SerializeField] Vector2 clipPlanes = new(0.3f, 1500f);

        [Header("The hangar (SQUADS)")]
        [Tooltip("Where each seat's vessel stands (seat 0, 1, 2), model space on the hangar floor: the squad lined up across the bay facing the camera (Rev 61 C).")]
        [SerializeField] Vector3[] padPositions =
            { new(0f, -23f, 66f), new(9f, -23f, 60.5f), new(-9.6f, -23f, 60.5f) };
        [Tooltip("The vessels' prefabs (Vessel Prefab Container): a squad vessel is drawn from its prefab's renderers alone, a picture with nothing of the vessel's play.")]
        [SerializeField] VesselPrefabContainer vesselPrefabs;
        [Tooltip("Every vessel is drawn this long (its longest side), whatever its class's size, so the line reads as three equals.")]
        [SerializeField, Min(1f)] float vesselLength = 8f;
        [Tooltip("How high a live vessel hovers over its pad (an offline one rests lower).")]
        [SerializeField, Min(0f)] float hoverHeight = 2.6f;
        [Tooltip("The pad's glow under a live vessel, in the squad's domain colour.")]
        [SerializeField, Min(0f)] float padLightIntensity = 4f;
        [Tooltip("How much of its colour a powered-down vessel keeps (an offline friend's).")]
        [SerializeField, Range(0f, 1f)] float offlineBrightness = 0.12f;

        [Header("The cell outside")]
        [Tooltip("Show the menu's own cell beyond the open mouth: captured once by a camera at the menu camera's place when the Ark opens (the menu is paused behind the Ark, so the cell holds still), shown on a backdrop. Off: the camera's skybox.")]
        [SerializeField] bool liveCell = true;
        [Tooltip("The backdrop's centre (model space, far beyond the pier's end) and its size.")]
        [SerializeField] Vector3 cellBackdropCentre = new(0f, -10f, 420f);
        [SerializeField] Vector2 cellBackdropSize = new(1500f, 850f);
        [Tooltip("The capture's pixel size.")]
        [SerializeField] Vector2Int cellCaptureSize = new(1280, 720);

        [Header("The pier (CONVERTER)")]
        [Tooltip("Where the pier leaves the hangar (model z of the mouth).")]
        [SerializeField] float pierStartZ = 88f;
        [Tooltip("Length out into the cell.")]
        [SerializeField, Min(10f)] float pierLength = 82f;
        [Tooltip("Width of the deck.")]
        [SerializeField, Min(2f)] float pierWidth = 20f;
        [Tooltip("Model y of the deck's top: the hangar's floor.")]
        [SerializeField] float pierTopY = -23f;
        [Tooltip("Model z of the round deck in the pier's middle, where the combine plays.")]
        [SerializeField] float deckZ = 128f;
        [Tooltip("Radius of the round deck.")]
        [SerializeField, Min(1f)] float deckRadius = 7.2f;
        [Tooltip("Metres between the lamp posts down each edge.")]
        [SerializeField, Min(2f)] float lampSpacing = 10f;
        [Tooltip("Height of the combine's centre above the deck.")]
        [SerializeField, Min(0f)] float combineHeight = 6.5f;
        [Tooltip("The combine's whole spread (its framing radius) is shrunk to this many metres, so the four crystals fit in the mouth's view over the deck.")]
        [SerializeField, Min(0.5f)] float combineRadius = 9f;

        [Header("The artifacts room (ARTIFACTS)")]
        [Tooltip("The room's floor centre, model space: well away from the Ark so nothing of the hull shows.")]
        [SerializeField] Vector3 artifactsRoomOrigin = new(0f, -20f, 600f);
        [Tooltip("Half width, half length and height of the gallery hall.")]
        [SerializeField] Vector3 hallSize = new(8f, 17f, 10f);
        [Tooltip("Radius of the Reliquary platform in the hall's middle, and of the arc its pedestals stand on.")]
        [SerializeField] Vector2 platformRadii = new(6.6f, 4.4f);
        [Tooltip("Alcoves down each wall of the hall: the slots for the artifacts still to be found.")]
        [SerializeField, Range(0, 8)] int alcovesPerWall = 4;
        [Tooltip("How large an artifact is drawn on its pedestal; the selected one is drawn larger by the factor after it.")]
        [SerializeField] Vector2 artifactScale = new(2.3f, 1.25f);

        [Header("Light")]
        [Tooltip("The cell's cold light falling on the pier and in at the mouth.")]
        [SerializeField] Color cellLight = new(0.55f, 0.68f, 1f);
        [SerializeField, Min(0f)] float cellLightIntensity = 1.2f;
        [Tooltip("The crystals' own light on the deck under them.")]
        [SerializeField] Color crystalLight = new(0.6f, 1f, 0.25f);
        [SerializeField, Min(0f)] float crystalLightIntensity = 3f;
        [Tooltip("The spot over each artifact (the selected one is twice as bright).")]
        [SerializeField, Min(0f)] float artifactSpotIntensity = 6f;
        [Tooltip("The clear colour behind everything (the cell's blue where the menu's live cell is not drawn).")]
        [SerializeField] Color cellBackground = new(0.12f, 0.32f, 0.62f);

        public ArkModelDefinitionSO ArkModel => arkModel;
        public Mesh PrismBoxMesh => prismBoxMesh;
        public float TopDoorDegrees => topDoorDegrees;
        public float BottomDoorDegrees => bottomDoorDegrees;
        public View HangarView => hangarView;
        public View PierView => pierView;
        public View ArtifactsView => artifactsView;
        public Vector2 ClipPlanes => clipPlanes;
        public IReadOnlyList<Vector3> PadPositions => padPositions;
        public VesselPrefabContainer VesselPrefabs => vesselPrefabs;
        public float VesselLength => vesselLength;
        public float HoverHeight => hoverHeight;
        public float PadLightIntensity => padLightIntensity;
        public float OfflineBrightness => offlineBrightness;
        public bool LiveCell => liveCell;
        public Vector3 CellBackdropCentre => cellBackdropCentre;
        public Vector2 CellBackdropSize => cellBackdropSize;
        public Vector2Int CellCaptureSize => cellCaptureSize;
        public float PierStartZ => pierStartZ;
        public float PierLength => pierLength;
        public float PierWidth => pierWidth;
        public float PierTopY => pierTopY;
        public float DeckZ => deckZ;
        public float DeckRadius => deckRadius;
        public float LampSpacing => lampSpacing;
        public float CombineHeight => combineHeight;
        public float CombineRadius => combineRadius;
        public Vector3 ArtifactsRoomOrigin => artifactsRoomOrigin;
        public Vector3 HallSize => hallSize;
        public Vector2 PlatformRadii => platformRadii;
        public int AlcovesPerWall => alcovesPerWall;
        public Vector2 ArtifactScale => artifactScale;
        // Colours are read through the colour trace: the worn colour set paints them (Docs/VesselModels/COLOR_TRACE.md).
        public Color CellLight => ColorTrace.Retrace(ColorTrace.World, cellLight);
        public float CellLightIntensity => cellLightIntensity;
        public Color CrystalLight => ColorTrace.Retrace(ColorTrace.World, crystalLight);
        public float CrystalLightIntensity => crystalLightIntensity;
        public float ArtifactSpotIntensity => artifactSpotIntensity;
        public Color CellBackground => ColorTrace.Retrace(ColorTrace.World, cellBackground);

        const string ResourcePath = "ArkInteriorConfig";

        /// <summary>The shipped config (Assets/Resources/ArkInteriorConfig.asset), for a view no one has wired yet. Null if absent.</summary>
        public static ArkInteriorConfigSO LoadDefault() => Resources.Load<ArkInteriorConfigSO>(ResourcePath);
    }
}
