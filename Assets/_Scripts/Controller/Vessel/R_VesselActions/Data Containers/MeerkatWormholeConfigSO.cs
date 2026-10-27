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

        public MeerkatWormholeMath.ShapeSettings Shape() => new()
        {
            ShaftMin = shaftMin, ShaftMax = shaftMax, BendRadius = bendRadius, BendMaxDegrees = bendMaxDegrees,
            ThroatRadius = 0.5f * throatDiameter, MouthRadius = 0.5f * mouthDiameter,   // round 10: authored as diameters
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
