using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The model an <see cref="CosmicShore.Gameplay.Ark"/> is drawn with and how it moves: the FBX, its rig table
    /// (<see cref="CosmicShore.Gameplay.ArkRigTable"/>: the sockets the hull's prisms regenerate into and the takes the
    /// jets and wings play), the Domain-slot material the Ark's domain replaces, how hard a turn must be to play a
    /// take in full, how the action takes play (the lance on a payload, the hangar for a squadmate in reach) and how
    /// fast the reactor's collar turns. Every Ark that is given one
    /// (<see cref="CosmicShore.Gameplay.Ark.UseModel"/>) is this model; an Ark without one keeps the procedural spindle hull.
    /// </summary>
    [CreateAssetMenu(fileName = "ArkModel", menuName = "ScriptableObjects/Toys/Ark Model")]
    public sealed class ArkModelDefinitionSO : ScriptableObject
    {
        [Header("Model")]
        [SerializeField, Tooltip("The Ark's FBX (Assets/_Models/Vessel Models/ArkRound.fbx; ArkReverse.fbx before 2026-11-25), instantiated under the Ark's root.")]
        GameObject model;

        [SerializeField, Tooltip("The model's rig table (ArkReverse.rig.json, written by the model's build beside the FBX): the sockets " +
                                 "and the takes, in model space. Rebuild the model and the table changes with it.")]
        TextAsset rigTable;

        [SerializeField, Min(0.05f), Tooltip("Uniform scale of the model (and its sockets and prisms). 1 = as built: 196 from nose to jets, " +
                                             "half a nucleus radius.")]
        float scale = 1f;

        [Header("Domain")]
        [SerializeField, Tooltip("Materials the Ark's domain replaces wherever the model wears them (the Domain slot's placeholder, " +
                                 "GreenAccentVesselMaterial) - the vessels' identity list, so the hull's trim wears the owner's colour.")]
        List<Material> domainReplacesMaterials = new();

        [Header("Look - how bright each slot is drawn (the Ark's own copies; the shared vessel materials are untouched)")]
        [SerializeField, Range(0f, 2f), Tooltip("The Domain slot (the domain's neon seams). Below 1 so a 196-unit ship reads as a hull, " +
                                                "not a lamp; the domain's colour still reads (owner, 2026-11-01: 'the ark is far too bright').")]
        float domainBrightness = 0.45f;

        [SerializeField, Range(0f, 2f), Tooltip("The Body slot (the gunmetal hull). At 1 the hull's rim colour reads pale, nearly " +
                                                "white, at escort range; 0.45 reads as a dark hull (checked on the escort sim's renders).")]
        float bodyBrightness = 0.45f;

        [SerializeField, Range(0f, 2f), Tooltip("The Window slot (port slits, the bridge visor, the lights).")]
        float windowBrightness = 0.55f;

        [SerializeField, Range(0f, 2f), Tooltip("The Engine slot (the jets' ember, clamp pads, running lights).")]
        float engineBrightness = 0.6f;

        [Header("Puppetry - how hard a turn plays a take in full")]
        [SerializeField, Min(1f), Tooltip("Yaw rate, degrees per second, that plays the yaw take in full (the Ark turns at up to 40).")]
        float yawRateFull = 40f;

        [SerializeField, Min(1f), Tooltip("Pitch rate, degrees per second, that plays the pitch take in full.")]
        float pitchRateFull = 40f;

        [SerializeField, Range(0f, 1f), Tooltip("How much of a yaw also plays the roll take (the jaws and the spoilers bank into the turn).")]
        float bankFromYaw = 0.8f;

        [SerializeField, Min(0.1f), Tooltip("Acceleration, units per second per second, that plays the speed takes in full (speed-up on the " +
                                            "way out of a cell, the air brake on the way in).")]
        float accelFull = 20f;

        [SerializeField, Min(0.01f), Tooltip("Seconds the jets and wings take to follow a change of course (exponential).")]
        float response = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("Scale on every take. Capped at 1: the model's clearance gate proved one full take per bone (a bone two inputs move shares it), not more.")]
        float amplitude = 1f;

        [Header("Helm - how the hull is sailed: way, steering, weave, lean, swell and flinch (ArkHelm)")]
        [SerializeField, Tooltip("How the Ark gathers way, swings its bow, weaves, banks, breathes at anchor and flinches when a " +
                                 "plate is struck. The course it sails is the same on every machine in a party.")]
        CosmicShore.Gameplay.ArkHelmSettings helm = CosmicShore.Gameplay.ArkHelmSettings.Default;

        [Header("Actions - the model's other takes, played on events")]
        [SerializeField, Min(0.1f), Tooltip("Seconds the lance stays out of its cradles after the hold hands a payload over.")]
        float releaseSeconds = 1.2f;

        [SerializeField, Min(0f), Tooltip("How close to the Ark a squadmate must be for the hangar to open. 0 = the hold's grab " +
                                          "reach (PayloadConfig), so the open hangar says a payload can be taken here.")]
        float hangarReach;

        [SerializeField, Min(0.01f), Tooltip("Seconds the actions take to follow (exponential).")]
        float actionResponse = 0.3f;

        [Header("Reactor - the omni crystal in the well (always one size)")]
        [SerializeField, Min(0f), Tooltip("Degrees per second the crystal's collar turns while the hull is whole.")]
        float collarSpinIdle = 12f;

        [SerializeField, Min(0f), Tooltip("Degrees per second the collar turns while the hull is regrowing.")]
        float collarSpinRegrowing = 90f;

        [SerializeField, Tooltip("The game's own omni crystal mesh, put on the model's omni bone when the rig table names a " +
                                 "reactorRadius (the round Ark): the model draws no crystal of its own. Empty = none.")]
        Mesh reactorCrystalMesh;

        [SerializeField, Tooltip("The crystal's material (the game's CrystalMaterial): its bright and dull crystal colours are " +
                                 "written from the colour set's call-to-action pair, the neutral lime every free omni crystal wears, " +
                                 "never a team colour.")]
        Material reactorCrystalMaterial;

        [Header("Hull - what a vessel cannot fly through")]
        [SerializeField, Tooltip("Which solid this model is to vessels (ArkHullSolid): Reverse = ArkReverse's hangar and ring " +
                                 "tunnel; Round = the round Ark's tube, junction hall and three stern exits.")]
        ArkHullShape hullShape = ArkHullShape.Reverse;

        [Header("Engine trails - the streams the rig table's trails leave (the round Ark's Trail 3)")]
        [SerializeField, Tooltip("The streams' material (additive, vertex-coloured). Empty = no streams.")]
        Material engineTrailMaterial;

        [SerializeField, Min(0.1f), Tooltip("Seconds a stream lasts: at the Ark's cruise (18) a 7 s stream is ~125 long.")]
        float engineTrailSeconds = 7f;

        [SerializeField, Range(0f, 1f), Tooltip("How opaque a stream is where it leaves its bell (it fades to nothing at its end).")]
        float engineTrailAlpha = 0.6f;

        [SerializeField, Range(0f, 2f), Tooltip("A stream's width at its end, as a share of its width at the bell.")]
        float engineTrailEndWidth = 0.4f;

        public GameObject Model => model;
        public Mesh ReactorCrystalMesh => reactorCrystalMesh;
        public Material ReactorCrystalMaterial => reactorCrystalMaterial;
        public ArkHullShape HullShape => hullShape;
        public Material EngineTrailMaterial => engineTrailMaterial;
        public float EngineTrailSeconds => engineTrailSeconds;
        public float EngineTrailAlpha => engineTrailAlpha;
        public float EngineTrailEndWidth => engineTrailEndWidth;
        public TextAsset RigTable => rigTable;
        public float Scale => scale;
        public CosmicShore.Gameplay.ArkHelmSettings Helm => helm;
        public IReadOnlyList<Material> DomainReplacesMaterials => domainReplacesMaterials;
        public float YawRateFull => yawRateFull;
        public float PitchRateFull => pitchRateFull;
        public float BankFromYaw => bankFromYaw;
        public float AccelFull => accelFull;
        public float Response => response;
        public float Amplitude => amplitude;
        public float ReleaseSeconds => releaseSeconds;
        public float HangarReach => hangarReach;
        public float ActionResponse => actionResponse;
        public float CollarSpinIdle => collarSpinIdle;
        public float CollarSpinRegrowing => collarSpinRegrowing;
        public float DomainBrightness => domainBrightness;
        public float BodyBrightness => bodyBrightness;
        public float WindowBrightness => windowBrightness;
        public float EngineBrightness => engineBrightness;
    }
}
