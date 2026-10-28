using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>How a formed Meerkat tunnel shows after RT.</summary>
    public enum MeerkatFormedLook { Bridge = 0, ContourField = 1 }

    /// <summary>The contour field's strength along the tube (the Tube Lens Lab's flow buttons).</summary>
    public enum MeerkatFieldFlow { Even = 0, InThenOut = 1, Throat = 2 }

    /// <summary>The contour field's fall-off across its shell (the Tube Lens Lab's shape buttons).</summary>
    public enum MeerkatFieldProfile { Smooth = 0, Smoother = 1, Linear = 2, Bell = 3 }

    /// <summary>
    /// Every number of the Meerkat's one ability, the WORMHOLE (<c>R_VesselActions/MEERKAT.md</c>). Cloned from the Vessel
    /// Studio (<c>Docs/Studios/VesselStudio/meerkat.html</c>, its <c>SHIPPED</c> block and <c>SPEC</c> rows): every field is
    /// the page's key by the same name, its <c>[Tooltip]</c> is the page's label word for word, and its default is the
    /// page's shipped value. <c>Tools/Build/studio_to_unity/meerkat.json</c> maps each key here; the artifact is the one
    /// source of truth, so tune it there and clone (<c>/artifact-to-unity</c>), never by hand in this asset.
    ///
    /// <para><b>The tree and the roots</b> (the artifact's rounds 13-21; <c>MEERKAT.md</c> §7): the <c>trunk*</c> and
    /// <c>branch*</c> keys draw the dead tree round every tunnel (<see cref="MeerkatTreeMesh"/>); the <c>omni*</c>,
    /// <c>labyrinth*</c> and <c>root*</c> keys are the Omnicrystal ability, the roots and the flight inside them
    /// (<see cref="MeerkatRootMath"/>). Not ported yet: <c>trunkWarp</c> (the tree bending space needs the lens pass),
    /// <c>omniAtOnce</c> 0 (an omnicrystal arming the next RT; the game always grows the roots at once), the soil
    /// (<c>soil*</c>), the dug wireframe (<c>labyrinthRing*</c>, <c>labyrinthRibs</c>, <c>labyrinthRoughness</c>) and the fork
    /// call-out (<c>forkHintSeconds</c>).</para>
    ///
    /// <para>Not here, because they shape only the lab: <c>aiTau</c> (the sim's own steering response; the game's AI
    /// flies with <c>AIPilot</c>), <c>cruise</c> / <c>pitchYaw</c> / <c>roll</c> (the hull's
    /// <c>MeerkatVesselTransformer</c> on the prefab) and the stand-in hull's <c>lope*</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MeerkatWormholeConfig", menuName = "ScriptableObjects/Vessel Actions/Meerkat Wormhole Config")]
    public sealed class MeerkatWormholeConfigSO : ScriptableObject
    {
        [Header("Wormhole shape (the artifact's Play Style Config > Wormhole shape)")]
        [Tooltip("The reticle rides this far ahead of the nose (u)")]
        [SerializeField, Range(0f, 100f)] float reticleAhead = 30f;
        [Tooltip("Sharpest bend, the right stick fully over (°)")]
        [SerializeField, Range(0f, 180f)] float bendMaxDegrees = 150f;
        [Tooltip("The straight shaft before the bend, LT released (u)")]
        [SerializeField, Range(0f, 600f)] float shaftMin = 60f;
        [Tooltip("...LT fully squeezed (u)")]
        [SerializeField, Range(20f, 1500f)] float shaftMax = 600f;
        [Tooltip("The bend's radius (u)")]
        [SerializeField, Range(10f, 300f)] float bendRadius = 60f;
        [Tooltip("The bend round the middle: how oval at the shortest tunnel (0 = a circle, 1 = a parabola)")]
        [SerializeField, Range(0f, 1.5f)] float ovalShortest = 0.2f;
        [Tooltip("...at the longest (over 1 = a hyperbola: straighter arms)")]
        [SerializeField, Range(0f, 1.5f)] float ovalLongest = 1.1f;
        [Tooltip("...but its middle turns at most this many times as sharply as its ends")]
        [SerializeField, Range(1f, 40f)] float ovalMaxSharpness = 8f;
        [Tooltip("Throat diameter, fixed (u)")]
        [SerializeField, Range(2f, 60f)] float throatDiameter = 8f;
        [Tooltip("Entry and exit diameter, fixed (u)")]
        [SerializeField, Range(4f, 120f)] float mouthDiameter = 26f;
        [Tooltip("LT on a key or touch: full after (s)")]
        [SerializeField, Range(0.05f, 3f)] float keyRampSeconds = 0.5f;

        [Header("Wormhole ride (the artifact's Play Style Config > Wormhole ride)")]
        [Tooltip("Ride: going in (× your speed before the tunnel)")]
        [SerializeField, Range(0.5f, 3f)] float rideEntryMultiplier = 1f;
        [Tooltip("Ride: fastest, at the throat (× your speed before the tunnel)")]
        [FormerlySerializedAs("throatSpeed")]
        [SerializeField, Range(0.5f, 8f)] float rideMidMultiplier = 2.5f;
        [Tooltip("Ride: coming out (× your speed before the tunnel)")]
        [SerializeField, Range(0.5f, 3f)] float rideExitMultiplier = 1.2f;
        [Tooltip("Riding: the speed tunnel's dolly zoom (× its normal strength)")]
        [SerializeField, Range(0f, 5f)] float rideSpeedTunnelScale = 5f;
        [Tooltip("The reticle comes back after you are out in (s)")]
        [SerializeField, Range(0f, 3f)] float reticleDelaySeconds = 0.5f;
        [Tooltip("...rippling into shape over (s)")]
        [SerializeField, Range(0.05f, 3f)] float reticleGrowSeconds = 0.8f;
        [Tooltip("RT: the tunnel ripples out of its centre over (s)")]
        [SerializeField, Range(0.05f, 2f)] float formSeconds = 0.4f;
        [Tooltip("It stays after you are out for (s)")]
        [SerializeField, Range(0f, 30f)] float staySeconds = 10f;
        [Tooltip("...then collapses back to its centre in (s)")]
        [SerializeField, Range(0.2f, 3f)] float collapseSeconds = 0.8f;

        [Header("Reticle feel & look (the artifact's Play Style Config > Reticle, yours alone)")]
        [Tooltip("Your reticle follows the sticks over (s, 0 = at once)")]
        [SerializeField, Range(0f, 1f)] float reticleSmoothSeconds = 0.15f;
        [Tooltip("Your reticle, right stick centred (brightness)")]
        [SerializeField, Range(0f, 1f)] float reticleIdleAlpha = 0.4f;
        [Tooltip("Your reticle's colour: your domain colour toward white")]
        [SerializeField, Range(0f, 1f)] float reticleWhite = 0.45f;
        [Tooltip("...its brightness at the way in, against its far end (1 = even)")]
        [SerializeField, Range(0f, 1f)] float reticleNearLight = 0.25f;

        [Header("Wormhole look (the artifact's Play Style Config > Wormhole look, Space look)")]
        [Tooltip("Space bent along the tube (strength)")]
        [SerializeField, Range(0f, 2f)] float warpStrength;
        [Tooltip("Space bent in at each mouth (strength)")]
        [SerializeField, Range(0f, 0.95f)] float dentStrength;
        [Tooltip("Its collapse ripples the screen's edges (strength)")]
        [SerializeField, Range(0f, 1f)] float rippleStrength;
        [Tooltip("...from this far out (share of the screen, 0 = centre)")]
        [SerializeField, Range(0f, 0.9f)] float rippleEdge = 0.45f;
        [Tooltip("...for (s)")]
        [SerializeField, Range(0.2f, 4f)] float rippleSeconds = 1.2f;
        [Tooltip("The formed tunnel's bridge, for everyone (0 = hidden)")]
        [SerializeField, Range(0f, 1f)] float bridgeVisibility = 1f;
        [Tooltip("...its glass surface (brightness)")]
        [SerializeField, Range(0f, 2f)] float bridgeGlass = 0.6f;
        [Tooltip("...rings of spindles along it")]
        [SerializeField, Range(2, 40)] int bridgeRings = 16;
        [Tooltip("...spindles round each ring")]
        [SerializeField, Range(3, 32)] int bridgeSpindlesPerRing = 12;
        [Tooltip("...ribs of spindles along it (0 = none)")]
        [SerializeField, Range(0, 16)] int bridgeRibs = 6;
        [Tooltip("...spindle thickness (u)")]
        [SerializeField, Range(0.1f, 6f)] float bridgeSpindleThickness = 1.6f;
        [Tooltip("...a shimmer running way in to way out")]
        [SerializeField, Range(0f, 1f)] float bridgeShimmer = 0.5f;
        [Tooltip("...its spindles' colour: the domain colour toward white")]
        [SerializeField, Range(0f, 1f)] float bridgeWhite = 0.45f;

        [Header("Tree (the artifact's Play Style Config > Wormhole · trunk: the tunnel's outer layer, round 14)")]
        [Tooltip("The tree: the tunnel's outer layer, for everyone (0 = hidden; off since round 23)")]
        [SerializeField, Range(0f, 1f)] float trunkVisibility = 0f;
        [Tooltip("Trunk at the way in (× the mouth's radius)")]
        [SerializeField, Range(1f, 3f)] float trunkTop = 1.5f;
        [Tooltip("Trunk at the way out (× the mouth's radius)")]
        [SerializeField, Range(1f, 3f)] float trunkBase = 1.9f;
        [Tooltip("Bark ridges: depth (× the radius)")]
        [SerializeField, Range(0f, 0.5f)] float trunkBark = 0.22f;
        [Tooltip("...ridges round the trunk")]
        [SerializeField, Range(3, 40)] int trunkRidges = 16;
        [Tooltip("...slow gnarled wobble")]
        [SerializeField, Range(0f, 0.3f)] float trunkGnarl = 0.14f;
        [Tooltip("Its glass face-on (the see-through middle)")]
        [SerializeField, Range(0f, 0.5f)] float trunkGlass = 0.012f;
        [Tooltip("Its glass at the silhouette (Fresnel)")]
        [SerializeField, Range(0f, 2f)] float trunkRim = 0.28f;
        [Tooltip("...how tight to the silhouette (power)")]
        [SerializeField, Range(0.5f, 8f)] float trunkFresnel = 3f;
        [Tooltip("Tree rings on the cut ends")]
        [SerializeField, Range(1, 20)] int trunkRings = 7;
        [Tooltip("Bark grain: its fissures' brightness")]
        [SerializeField, Range(0f, 2f)] float trunkGrain = 0.22f;
        [Tooltip("...fissures round the wood")]
        [SerializeField, Range(4, 60)] int trunkGrainLines = 22;
        [Tooltip("Branches, in pairs")]
        [SerializeField, Range(0, 16)] int branchCount = 6;
        [Tooltip("...their length (× the mouth's radius)")]
        [SerializeField, Range(0f, 4f)] float branchLength = 2.4f;
        [Tooltip("...thickness at the base (× the mouth's radius)")]
        [SerializeField, Range(0.02f, 0.8f)] float branchThickness = 0.28f;
        [Tooltip("...leaning toward the top (°)")]
        [SerializeField, Range(0f, 80f)] float branchUp = 28f;
        [Tooltip("...out of the trunk's top (share of its length)")]
        [SerializeField, Range(0.1f, 1f)] float branchZone = 0.85f;
        [Tooltip("...each pair mirrored (1) or each its own (0)")]
        [SerializeField, Range(0f, 1f)] float branchSymmetry = 0.45f;
        [Tooltip("...times each forks")]
        [SerializeField, Range(0, 3)] int branchForks = 2;
        [Tooltip("...each grows over (s), once the tunnel reaches it")]
        [SerializeField, Range(0.1f, 4f)] float branchGrowSeconds = 0.5f;

        [Header("Omnicrystal · the wormhole network (the artifact's Play Style Config > Omnicrystal, rounds 16-23)")]
        [Tooltip("The Omnicrystal ability: an omnicrystal opens the wormhole network (0 = off)")]
        [SerializeField] bool omniOn = true;
        [Tooltip("Omnicrystals on the course (a new course or reset places them)")]
        [SerializeField, Range(0, 12)] int omniCount = 3;
        [Tooltip("The network: forks after the smallest tunnel")]
        [SerializeField, Range(1, 16)] int labyrinthBranchPointsMin = 3;
        [Tooltip("...forks after the biggest tunnel")]
        [SerializeField, Range(1, 24)] int labyrinthBranchPointsMax = 12;
        [Tooltip("...openings after the smallest tunnel")]
        [SerializeField, Range(2, 48)] int labyrinthOpeningsMin = 5;
        [Tooltip("...openings after the biggest tunnel")]
        [SerializeField, Range(2, 72)] int labyrinthOpeningsMax = 30;
        [Tooltip("...reach after the smallest tunnel (u)")]
        [SerializeField, Range(20f, 900f)] float labyrinthRadiusMin = 90f;
        [Tooltip("...reach after the biggest tunnel (u)")]
        [SerializeField, Range(40f, 1500f)] float labyrinthRadiusMax = 420f;
        [Tooltip("...its openings spread round it over (°, 360 = all round)")]
        [SerializeField, Range(20f, 360f)] float labyrinthSpread = 360f;
        [Tooltip("...randomness")]
        [SerializeField, Range(0f, 1f)] float labyrinthRandomness = 0.45f;
        [Tooltip("...each wormhole's diameter (u)")]
        [SerializeField, Range(2f, 60f)] float labyrinthDiameter = 40f;
        [Tooltip("...source and opening diameter (u)")]
        [SerializeField, Range(4f, 120f)] float labyrinthMouthDiameter = 46f;
        [Tooltip("The wormholes taper to (× the one at the source)")]
        [SerializeField, Range(0.1f, 1f)] float rootTip = 0.6f;
        [Tooltip("...how much each wormhole wanders")]
        [SerializeField, Range(0f, 1f)] float rootGnarl = 0.3f;
        [Tooltip("The network's root bark, for everyone (0 = hidden: the channels are wormhole tubes; off since round 23)")]
        [SerializeField, Range(0f, 1f)] float rootBarkVisibility = 0f;
        [Tooltip("...their bark face-on")]
        [SerializeField, Range(0f, 0.3f)] float rootGlass = 0.015f;
        [Tooltip("...their bark at the silhouette (Fresnel)")]
        [SerializeField, Range(0f, 2f)] float rootRim = 0.35f;
        [Tooltip("...their bark grain")]
        [SerializeField, Range(0f, 1.5f)] float rootGrain = 0.35f;
        [Tooltip("...digs itself out from the source over (s)")]
        [SerializeField, Range(0.05f, 4f)] float labyrinthFormSeconds = 0.8f;
        [Tooltip("In the network: you cruise along the tube at (u/s)")]
        [SerializeField, Range(0f, 200f)] float rootCruise = 45f;
        [Tooltip("...RT pushes you faster by (u/s²)")]
        [SerializeField, Range(0f, 300f)] float rootThrust = 90f;
        [Tooltip("...the left stick moves you round the tube by (u/s²)")]
        [SerializeField, Range(0f, 200f)] float rootMove = 55f;
        [Tooltip("...across the tube, your drift dies away at (per s)")]
        [SerializeField, Range(0f, 3f)] float rootDrag = 0.45f;
        [Tooltip("...never faster than (u/s)")]
        [SerializeField, Range(10f, 300f)] float rootMaxSpeed = 90f;
        [Tooltip("...a wall bounces you off with (× your speed into it)")]
        [SerializeField, Range(0f, 1f)] float rootBounce = 0.45f;
        [Tooltip("...drawn to the tube's floor by (u/s², 0 = you float)")]
        [SerializeField, Range(0f, 120f)] float rootGravity = 0f;
        [Tooltip("From inside: the roots' old inner wall (0 = off, the tube's calm wall instead; off since round 23)")]
        [SerializeField, Range(0f, 1f)] float rootOpacity = 0f;
        [Tooltip("Every tube's calm inner wall (a tunnel's and each channel's): a smooth sheen")]
        [SerializeField, Range(0f, 0.6f)] float tubeInner = 0.1f;
        [Tooltip("...your lamp on the walls round you")]
        [SerializeField, Range(0f, 3f)] float rootLamp = 1f;
        [Tooltip("...far walls fade to clear this far off (u)")]
        [SerializeField, Range(20f, 600f)] float rootFog = 240f;

        [Header("After RT (Garrett, 2026-10-27: the Tube Lens Lab)")]
        [Tooltip("After RT, a formed tunnel shows as: ContourField = nothing drawn, only space bent in a thin shell on its " +
                 "wall (Garrett: \"lets the lensing alone sell the effect\"); Bridge = round 3's spindle bridge and glass.")]
        [SerializeField] MeerkatFormedLook formedLook = MeerkatFormedLook.ContourField;

        [Header("Contour field (the Tube Lens Lab's gateField* keys, Docs/Studios/StoatTubeLensLab.html)")]
        [Tooltip("Strength (bend per wall crossing, °)")]
        [SerializeField, Range(0f, 40f)] float fieldStrength = 18f;
        [Tooltip("Pitch: along the tube 0°, round it 90° (°)")]
        [SerializeField, Range(0f, 90f)] float fieldPitch = 90f;
        [Tooltip("Drag: pull rays along the field lines")]
        [SerializeField, Range(0f, 2f)] float fieldDrag = 1f;
        [Tooltip("Twist: turn rays round the field lines")]
        [SerializeField, Range(0f, 2f)] float fieldTwist;
        [Tooltip("Glass: refract across the shell")]
        [SerializeField, Range(0f, 2f)] float fieldGlass = 0.4f;
        [Tooltip("Glass index at the wall (Δn)")]
        [SerializeField, Range(0f, 0.25f)] float fieldGlassIndex = 0.04f;
        [Tooltip("Grazing: 0 physical, 1 every crossing bends alike")]
        [SerializeField, Range(0f, 1f)] float fieldGrazing = 0.6f;
        [Tooltip("Flow along the tube: Even, In then out (bent in at the way in, out at the way out), Throat")]
        [SerializeField] MeerkatFieldFlow fieldFlow = MeerkatFieldFlow.InThenOut;
        [Tooltip("Inner thickness (× local radius)")]
        [SerializeField, Range(0f, 0.6f)] float fieldInner = 0.1f;
        [Tooltip("Outer thickness (× local radius)")]
        [SerializeField, Range(0f, 1f)] float fieldOuter = 0.12f;
        [Tooltip("Fall-off shape across the shell, from the wall to zero at its edge")]
        [SerializeField] MeerkatFieldProfile fieldProfile = MeerkatFieldProfile.Smooth;
        [Tooltip("Ribs: concentrate the field on the grid lines")]
        [SerializeField, Range(0f, 1f)] float fieldRibs = 0.9f;
        [Tooltip("Rib sharpness")]
        [SerializeField, Range(1f, 24f)] float fieldRibSharpness = 8f;
        [Tooltip("Ribs travel toward the exit (rings per second)")]
        [SerializeField, Range(-4f, 4f)] float fieldRibTravel = 1.5f;
        [Tooltip("Rib rings along the tube (the studio grid's WH_RINGS)")]
        [SerializeField, Range(2, 64)] int fieldRibRings = 22;
        [Tooltip("Rib lines round the tube (the studio grid's WH_LONG)")]
        [SerializeField, Range(1, 48)] int fieldRibLines = 12;

        [Header("AI (the artifact's AI tab)")]
        [Tooltip("AI uses the wormhole (0 = no, 1 = yes)")]
        [SerializeField] bool aiWormhole = true;
        [Tooltip("AI: a straight tunnel with its ring at least (u) away")]
        [SerializeField, Range(50f, 2000f)] float aiStraightMinDistance = 300f;
        [Tooltip("AI: a bending one from (u) away")]
        [SerializeField, Range(50f, 2000f)] float aiBentMinDistance = 120f;
        [Tooltip("AI: shapes the reticle for (s) before RT")]
        [SerializeField, Range(0.05f, 2f)] float aiShapeSeconds = 0.3f;
        [Tooltip("AI sloppiness (0 = perfect, 1 = rookie)")]
        [SerializeField, Range(0f, 1f)] float aiNoise;

        [Header("Audio (FMOD; shipped empty until sound design wires them)")]
        [Tooltip("RT formed a tunnel (at the hull).")]
        [SerializeField] EventReference formEvent;
        [Tooltip("A tunnel collapsed (at its centre).")]
        [SerializeField] EventReference collapseEvent;
        [Tooltip("An omnicrystal was taken and the roots grew (at the hull).")]
        [SerializeField] EventReference rootsEvent;

        public float ReticleAhead => reticleAhead;
        public float KeyRampSeconds => keyRampSeconds;
        public float RideEntryMultiplier => rideEntryMultiplier;
        public float RideMidMultiplier => rideMidMultiplier;
        public float RideExitMultiplier => rideExitMultiplier;
        public float RideSpeedTunnelScale => rideSpeedTunnelScale;
        public float ReticleDelaySeconds => reticleDelaySeconds;
        public float ReticleGrowSeconds => reticleGrowSeconds;
        public float FormSeconds => formSeconds;
        public float StaySeconds => staySeconds;
        public float CollapseSeconds => collapseSeconds;
        public float WarpStrength => warpStrength;
        public float DentStrength => dentStrength;
        public float RippleStrength => rippleStrength;
        public float RippleEdge => rippleEdge;
        public float RippleSeconds => rippleSeconds;
        public float ReticleSmoothSeconds => reticleSmoothSeconds;
        public float ReticleIdleAlpha => reticleIdleAlpha;
        public float ReticleWhite => reticleWhite;
        public float ReticleNearLight => reticleNearLight;
        public float BridgeVisibility => bridgeVisibility;
        public float BridgeGlass => bridgeGlass;
        public int BridgeRings => bridgeRings;
        public int BridgeSpindlesPerRing => bridgeSpindlesPerRing;
        public int BridgeRibs => bridgeRibs;
        public float BridgeSpindleThickness => bridgeSpindleThickness;
        public float BridgeShimmer => bridgeShimmer;
        public float BridgeWhite => bridgeWhite;
        public MeerkatFormedLook FormedLook => formedLook;
        public float FieldStrength => fieldStrength;
        public float FieldPitch => fieldPitch;
        public float FieldDrag => fieldDrag;
        public float FieldTwist => fieldTwist;
        public float FieldGlass => fieldGlass;
        public float FieldGlassIndex => fieldGlassIndex;
        public float FieldGrazing => fieldGrazing;
        public MeerkatFieldFlow FieldFlow => fieldFlow;
        public float FieldInner => fieldInner;
        public float FieldOuter => fieldOuter;
        public MeerkatFieldProfile FieldProfile => fieldProfile;
        public float FieldRibs => fieldRibs;
        public float FieldRibSharpness => fieldRibSharpness;
        public float FieldRibTravel => fieldRibTravel;
        public int FieldRibRings => fieldRibRings;
        public int FieldRibLines => fieldRibLines;
        /// <summary>A formed tunnel bends space through the contour field (the look is on and some coupling bends).</summary>
        public bool ContourFieldOn => formedLook == MeerkatFormedLook.ContourField &&
                                      (fieldStrength > 0f && (fieldDrag > 0f || fieldTwist > 0f) ||
                                       fieldGlass > 0f && fieldGlassIndex > 0f);
        public bool AiWormhole => aiWormhole;
        public float AiStraightMinDistance => aiStraightMinDistance;
        public float AiBentMinDistance => aiBentMinDistance;
        public float AiShapeSeconds => aiShapeSeconds;
        public float AiNoise => aiNoise;
        public EventReference FormEvent => formEvent;
        public EventReference CollapseEvent => collapseEvent;
        public EventReference RootsEvent => rootsEvent;
        public float TrunkVisibility => trunkVisibility;
        public float TrunkTop => trunkTop;
        public float TrunkBase => trunkBase;
        public float TrunkBark => trunkBark;
        public int TrunkRidges => trunkRidges;
        public float TrunkGnarl => trunkGnarl;
        public float TrunkGlass => trunkGlass;
        public float TrunkRim => trunkRim;
        public float TrunkFresnel => trunkFresnel;
        public int TrunkRings => trunkRings;
        public float TrunkGrain => trunkGrain;
        public int TrunkGrainLines => trunkGrainLines;
        public int BranchCount => branchCount;
        public float BranchLength => branchLength;
        public float BranchThickness => branchThickness;
        public float BranchUp => branchUp;
        public float BranchZone => branchZone;
        public float BranchSymmetry => branchSymmetry;
        public int BranchForks => branchForks;
        public float BranchGrowSeconds => branchGrowSeconds;
        public bool OmniOn => omniOn;
        public int OmniCount => omniCount;
        public int LabyrinthBranchPointsMin => labyrinthBranchPointsMin;
        public int LabyrinthBranchPointsMax => labyrinthBranchPointsMax;
        public int LabyrinthOpeningsMin => labyrinthOpeningsMin;
        public int LabyrinthOpeningsMax => labyrinthOpeningsMax;
        public float LabyrinthRadiusMin => labyrinthRadiusMin;
        public float LabyrinthRadiusMax => labyrinthRadiusMax;
        public float LabyrinthSpread => labyrinthSpread;
        public float LabyrinthRandomness => labyrinthRandomness;
        public float LabyrinthDiameter => labyrinthDiameter;
        public float LabyrinthMouthDiameter => labyrinthMouthDiameter;
        public float RootTip => rootTip;
        public float RootGnarl => rootGnarl;
        public float RootGlass => rootGlass;
        public float RootRim => rootRim;
        public float RootGrain => rootGrain;
        public float LabyrinthFormSeconds => labyrinthFormSeconds;
        public float RootCruise => rootCruise;
        public float RootThrust => rootThrust;
        public float RootMove => rootMove;
        public float RootDrag => rootDrag;
        public float RootMaxSpeed => rootMaxSpeed;
        public float RootBounce => rootBounce;
        public float RootGravity => rootGravity;
        public float RootOpacity => rootOpacity;
        /// <summary>Round 23: the roots' bark round each channel (0, shipped: every channel is a wormhole tube).</summary>
        public float RootBarkVisibility => rootBarkVisibility;
        public float TubeInner => tubeInner;
        public float RootLamp => rootLamp;
        public float RootFog => rootFog;

        /// <summary>The roots' layout keys (<see cref="MeerkatRootMath.BuildLayout"/>).</summary>
        public MeerkatRootMath.LayoutSettings RootLayout() => new()
        {
            BranchPointsMin = labyrinthBranchPointsMin, BranchPointsMax = labyrinthBranchPointsMax, OpeningsMin = labyrinthOpeningsMin,
            OpeningsMax = labyrinthOpeningsMax, RadiusMin = labyrinthRadiusMin, RadiusMax = labyrinthRadiusMax, Spread = labyrinthSpread,
            Randomness = labyrinthRandomness,
        };

        /// <summary>A root's size keys (<see cref="MeerkatRootMath.Build"/>).</summary>
        public MeerkatRootMath.ChannelSettings RootChannel() => new()
        {
            Diameter = labyrinthDiameter, MouthDiameter = labyrinthMouthDiameter, Gnarl = rootGnarl, Tip = rootTip,
        };

        /// <summary>The flight inside the roots (<see cref="MeerkatRootMath.Step"/>).</summary>
        public MeerkatRootMath.FlightSettings RootFlight() => new()
        {
            Cruise = rootCruise, Thrust = rootThrust, Move = rootMove, Drag = rootDrag, MaxSpeed = rootMaxSpeed, Bounce = rootBounce,
            Gravity = rootGravity, Tip = rootTip, ExitMultiplier = rideExitMultiplier,
        };

        /// <summary>The tree's keys (<see cref="MeerkatTreeMesh.BuildTree"/>).</summary>
        public MeerkatTreeMesh.TreeSettings Tree() => new()
        {
            Top = trunkTop, Base = trunkBase, Bark = trunkBark, Ridges = trunkRidges, Gnarl = trunkGnarl, BranchCount = branchCount,
            BranchLength = branchLength, BranchThickness = branchThickness, BranchUp = branchUp, BranchZone = branchZone,
            BranchSymmetry = branchSymmetry, BranchForks = branchForks,
        };

        public MeerkatWormholeMath.ShapeSettings Shape() => new()
        {
            ShaftMin = shaftMin, ShaftMax = shaftMax, BendRadius = bendRadius, BendMaxDegrees = bendMaxDegrees,
            ThroatRadius = 0.5f * throatDiameter, MouthRadius = 0.5f * mouthDiameter,   // round 10: authored as diameters
            OvalShortest = ovalShortest, OvalLongest = ovalLongest, OvalMaxSharpness = ovalMaxSharpness,   // round 22: the oval arc
        };

        /// <summary>The AI's plan rows; <paramref name="turnRateDegrees"/> is the hull's own turn (its transformer's YawScaler,
        /// the studio's pitchYaw).</summary>
        public MeerkatWormholeMath.PlanSettings Plan(float turnRateDegrees) => new()
        {
            ReticleAhead = reticleAhead, ShapeSeconds = aiShapeSeconds, Shape = Shape(),
            MeanRideMultiplier = MeerkatWormholeMath.MeanRideMultiplier(rideEntryMultiplier, rideMidMultiplier, rideExitMultiplier),
            TurnRateRadians = turnRateDegrees * Mathf.Deg2Rad,
        };
    }
}
