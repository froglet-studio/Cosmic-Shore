using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
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
        [SerializeField, Range(0f, 180f)] float bendMaxDegrees = 120f;
        [Tooltip("A straight tunnel is this long (u)")]
        [SerializeField, Range(60f, 1500f)] float lengthStraight = 600f;
        [Tooltip("The sharpest bend's tunnel is this long (u)")]
        [SerializeField, Range(20f, 600f)] float lengthBent = 120f;
        [Tooltip("Throat radius, straight (u)")]
        [SerializeField, Range(1f, 40f)] float throatStraight = 3f;
        [Tooltip("Throat radius, sharpest bend (u)")]
        [SerializeField, Range(5f, 120f)] float throatBent = 40f;
        [Tooltip("Mouth radius (× throat)")]
        [SerializeField, Range(1f, 5f)] float mouthFlare = 2f;
        [Tooltip("LT on a key or touch: full after (s)")]
        [SerializeField, Range(0.05f, 3f)] float keyRampSeconds = 0.5f;

        [Header("Wormhole ride (the artifact's Play Style Config > Wormhole ride)")]
        [Tooltip("Fastest at the throat (× your entry speed)")]
        [SerializeField, Range(1f, 8f)] float throatSpeed = 2.5f;
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
        [Tooltip("Your reticle's glass: a faint lit surface under it")]
        [SerializeField, Range(0f, 2f)] float reticleGlass = 0.15f;
        [Tooltip("...rings of spindles round it")]
        [SerializeField, Range(2, 30)] int reticleRings = 10;
        [Tooltip("...spindles round each ring")]
        [SerializeField, Range(3, 32)] int reticleSpindlesPerRing = 16;
        [Tooltip("...ribs of spindles along it (0 = none)")]
        [SerializeField, Range(0, 16)] int reticleRibs = 8;
        [Tooltip("...spindle thickness (u)")]
        [SerializeField, Range(0.05f, 3f)] float reticleSpindleThickness = 0.35f;
        [Tooltip("...the lime line you will fly (brightness, 0 = none)")]
        [SerializeField, Range(0f, 1f)] float reticlePath = 0.5f;

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
        public float ThroatSpeed => throatSpeed;
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
        public float ReticleGlass => reticleGlass;
        public int ReticleRings => reticleRings;
        public int ReticleSpindlesPerRing => reticleSpindlesPerRing;
        public int ReticleRibs => reticleRibs;
        public float ReticleSpindleThickness => reticleSpindleThickness;
        public float ReticlePath => reticlePath;
        public float BridgeVisibility => bridgeVisibility;
        public float BridgeGlass => bridgeGlass;
        public int BridgeRings => bridgeRings;
        public int BridgeSpindlesPerRing => bridgeSpindlesPerRing;
        public int BridgeRibs => bridgeRibs;
        public float BridgeSpindleThickness => bridgeSpindleThickness;
        public float BridgeShimmer => bridgeShimmer;
        public bool AiWormhole => aiWormhole;
        public float AiStraightMinDistance => aiStraightMinDistance;
        public float AiBentMinDistance => aiBentMinDistance;
        public float AiShapeSeconds => aiShapeSeconds;
        public float AiNoise => aiNoise;
        public EventReference FormEvent => formEvent;
        public EventReference CollapseEvent => collapseEvent;

        public MeerkatWormholeMath.ShapeSettings Shape() => new()
        {
            LengthStraight = lengthStraight, LengthBent = lengthBent, BendMaxDegrees = bendMaxDegrees,
            ThroatStraight = throatStraight, ThroatBent = throatBent, MouthFlare = mouthFlare,
        };

        public MeerkatWormholeMath.PlanSettings Plan() => new()
        {
            ReticleAhead = reticleAhead, ShapeSeconds = aiShapeSeconds, BendMaxDegrees = bendMaxDegrees,
            LengthStraight = lengthStraight, LengthBent = lengthBent,
        };
    }
}
