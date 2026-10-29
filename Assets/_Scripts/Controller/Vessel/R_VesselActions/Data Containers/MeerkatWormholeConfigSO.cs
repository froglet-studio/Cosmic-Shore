using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Meerkat's one ability, the WORMHOLE (<c>R_VesselActions/MEERKAT.md</c>). Cloned from the Vessel
    /// Studio (<c>Docs/Studios/VesselStudio/meerkat.html</c>, its <c>SHIPPED</c> block and <c>SPEC</c> rows): every plain field is
    /// the page's key by the same name, its <c>[Tooltip]</c> is the page's label word for word, and its default is the
    /// page's shipped value. <c>Tools/Build/studio_to_unity/meerkat.json</c> maps each key here; the artifact is the one
    /// source of truth for those, so tune it there and clone (<c>/artifact-to-unity</c>).
    ///
    /// <para><b>The wormhole tree</b> (Yash, 2026-10-29): the TRUNK is a formed tunnel (RT), the ROOT is a channel of an
    /// omnicrystal's network. Both look alike and are tuned alike: the same <see cref="MeerkatWormholeLook"/> and
    /// <see cref="MeerkatWormholeRideView"/>, one copy each (Trunk Look / Root Look, Trunk Ride / Root Ride), under one
    /// <see cref="MeerkatWormholeComfort"/> master. Those are Unity-side (the Vessel Studio has no such keys); every field,
    /// default and range: <c>Docs/Shaders/WORMHOLE_SHADERS.md</c> §14. The groups: Comfort, Trunk Look, Trunk Shape, Trunk
    /// Ride, Root Look, Root Shape, Root Ride, Kit (Garrett's), Miscellaneous (the reticle, the AI, audio).</para>
    ///
    /// <para>Removed 2026-10-29 (unused, or the water look; §14 lists them and their restore commit): the space-look keys
    /// (<c>warpStrength</c>, <c>dentStrength</c>, <c>ripple*</c>), the bridge's <c>bridgeVisibility</c> / <c>bridgeGlass</c> /
    /// <c>bridgeWhite</c>, the tree (<c>trunk*</c>, <c>branch*</c>), the roots' old skin and inner wall (<c>rootBarkVisibility</c>,
    /// <c>rootGlass</c>, <c>rootRim</c>, <c>rootGrain</c>, <c>rootOpacity</c>, <c>tubeInner</c>, <c>rootLamp</c>, <c>rootFog</c>)
    /// and the Tube Lens Lab's contour field (<c>field*</c>, folded into <see cref="MeerkatWormholeLook"/>). Not ported:
    /// <c>trunkWarp</c>, <c>omniAtOnce</c> 0, the soil (<c>soil*</c>), the dug wireframe (<c>labyrinthRing*</c>,
    /// <c>labyrinthRibs</c>, <c>labyrinthRoughness</c>) and the fork call-out (<c>forkHintSeconds</c>).</para>
    ///
    /// <para>Not here, because they shape only the lab: <c>aiTau</c> (the sim's own steering response; the game's AI
    /// flies with <c>AIPilot</c>), <c>cruise</c> / <c>pitchYaw</c> / <c>roll</c> (the hull's
    /// <c>MeerkatVesselTransformer</c> on the prefab) and the stand-in hull's <c>lope*</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MeerkatWormholeConfig", menuName = "ScriptableObjects/Vessel Actions/Meerkat Wormhole Config")]
    public sealed class MeerkatWormholeConfigSO : ScriptableObject
    {
        [Header("Comfort (every wormhole's look, trunk and root: keep riders from feeling sick)")]
        [SerializeField] MeerkatWormholeComfort comfort = MeerkatWormholeComfort.Default;

        [Header("Trunk Look (a formed tunnel: the clear jelly hollow, from outside and in)")]
        [SerializeField] MeerkatWormholeLook trunkLook = MeerkatWormholeLook.TrunkDefault;

        [Header("Trunk Shape (the artifact's Wormhole shape, and its life)")]
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
        [Tooltip("RT: the tunnel ripples out of its centre over (s)")]
        [SerializeField, Range(0.05f, 2f)] float formSeconds = 0.4f;
        [Tooltip("It stays after you are out for (s)")]
        [SerializeField, Range(0f, 30f)] float staySeconds = 10f;
        [Tooltip("...then collapses back to its centre in (s)")]
        [SerializeField, Range(0.2f, 3f)] float collapseSeconds = 0.8f;

        [Header("Trunk Ride (inside a tunnel: the view, and the artifact's Wormhole ride; the slide itself is Root Ride's flight)")]
        [SerializeField] MeerkatWormholeRideView trunkRide = MeerkatWormholeRideView.Default;
        [Tooltip("Ride: coming out (× your speed before the tunnel)")]
        [SerializeField, Range(0.5f, 3f)] float rideExitMultiplier = 1.2f;
        [Tooltip("Riding: the speed tunnel's dolly zoom (× its normal strength)")]
        [SerializeField, Range(0f, 5f)] float rideSpeedTunnelScale = 5f;

        [Header("Root Look (a channel of an omnicrystal's network: the trunk's look, its own numbers)")]
        [SerializeField] MeerkatWormholeLook rootLook = MeerkatWormholeLook.RootDefault;

        [Header("Root Shape (an omnicrystal's network: the artifact's Omnicrystal)")]
        [Tooltip("The Omnicrystal ability: an omnicrystal opens the wormhole network (0 = off)")]
        [SerializeField] bool omniOn = true;
        [Tooltip("Perf (2026-10-29): the menu's autopilot Meerkat (the lava lamp behind Menu_Main) grows root networks from " +
                 "omnicrystals too. Off: it takes none, so the lava lamp keeps its frame rate; a person flying in freestyle, " +
                 "and every match, grow them as ever")]
        [SerializeField] bool menuAutopilotRoots = false;
        [Tooltip("Round 26 (Yash, 2026-10-28: \"it will always be large ... if it has full mass, then it will be the largest, " +
                 "covering the entire cell\"): the network's reach as a share of its cell's radius (MeerkatNetworkCell), on Mass - " +
                 "Min the page's networkReachAtZero (0.5, at Mass 0), Max its networkReachAtFull (1, the whole cell, at Mass 10). " +
                 "Read when the omnicrystal is taken (EvaluateLive). An ElementalFloat block, so studio_to_unity.py cannot write it: " +
                 "keep it equal to the page's two keys by hand.")]
        [SerializeField] ElementalFloat networkReach = ElementalFloat.Multiplier(0.5f, 1f, Element.Mass, 0.05f);
        [Tooltip("Omnicrystals on the course (a new course or reset places them)")]
        [SerializeField, Range(0, 12)] int omniCount = 3;
        [Tooltip("...forks at its smallest (the least reach above)")]
        [SerializeField, Range(1, 16)] int labyrinthBranchPointsMin = 6;
        [Tooltip("...forks at its largest (the reach above)")]
        [SerializeField, Range(1, 24)] int labyrinthBranchPointsMax = 22;
        [Tooltip("...openings at its smallest")]
        [SerializeField, Range(2, 48)] int labyrinthOpeningsMin = 12;
        [Tooltip("...openings at its largest")]
        [SerializeField, Range(2, 72)] int labyrinthOpeningsMax = 56;
        [Tooltip("...it reaches at least (u), however low your Mass")]
        [SerializeField, Range(20f, 900f)] float labyrinthRadiusMin = 240f;
        [Tooltip("...a network reaching this far (u) has its most forks and openings")]
        [SerializeField, Range(40f, 1500f)] float labyrinthRadiusMax = 1080f;
        [Tooltip("...its openings spread round it over (°, 360 = all round)")]
        [SerializeField, Range(20f, 360f)] float labyrinthSpread = 360f;
        [Tooltip("...randomness")]
        [SerializeField, Range(0f, 1f)] float labyrinthRandomness = 0.45f;
        [Tooltip("...each wormhole's diameter (u)")]
        [SerializeField, Range(2f, 60f)] float labyrinthDiameter = 40f;
        [Tooltip("...source and opening diameter (u)")]
        [SerializeField, Range(4f, 120f)] float labyrinthMouthDiameter = 46f;
        [Tooltip("The network's wormholes taper to (× the one at its source)")]
        [SerializeField, Range(0.1f, 1f)] float rootTip = 0.6f;
        [Tooltip("...how much each wormhole wanders")]
        [SerializeField, Range(0f, 1f)] float rootGnarl = 0.3f;
        [Tooltip("...digs itself out from the source over (s)")]
        [SerializeField, Range(0.05f, 4f)] float labyrinthFormSeconds = 0.8f;

        [Header("Root Ride (inside a channel: the view; the flight, a tunnel's slide too)")]
        [SerializeField] MeerkatWormholeRideView rootRide = MeerkatWormholeRideView.Default;
        [Tooltip("In a wormhole or the network: you cruise along it at (u/s)")]
        [SerializeField, Range(0f, 200f)] float rootCruise = 45f;
        [Tooltip("...RT pushes you faster by (u/s²)")]
        [SerializeField, Range(0f, 300f)] float rootThrust = 90f;
        [Tooltip("...the left stick moves you round it by (u/s²)")]
        [SerializeField, Range(0f, 200f)] float rootMove = 55f;
        [Tooltip("...across it, your drift dies away at (per s)")]
        [SerializeField, Range(0f, 3f)] float rootDrag = 0.45f;
        [Tooltip("...never faster than (u/s)")]
        [SerializeField, Range(10f, 300f)] float rootMaxSpeed = 90f;
        [Tooltip("...a wall bounces you off with (× your speed into it)")]
        [SerializeField, Range(0f, 1f)] float rootBounce = 0.45f;
        [Tooltip("...drawn to the tube's floor by (u/s², 0 = you float)")]
        [SerializeField, Range(0f, 120f)] float rootGravity = 0f;

        // Round 23 (Yash, 2026-10-28: "revert the mass ability as Garrett has said ... the prism gets sucked in from this one
        // side and gets sucked out on the other ... distorted while traveling through this wormhole ... From a certain
        // radius"): Garrett's soliton. Space sets its surface area, so a longer tunnel is thinner; it pulls the prisms and
        // crystals round its way in, carries them through stretched and twisted, and fires them out of its way out.
        [Header("Kit · Space · the soliton (the artifact's Play Style Config > Kit · Space · the soliton, round 23)")]
        [Tooltip("Surface area, Space L0 (u²)")]
        [SerializeField, Range(1000f, 60000f)] float areaL0 = 6000f;
        [Tooltip("...at Space L10 (u²)")]
        [SerializeField, Range(1000f, 60000f)] float areaL10 = 18000f;
        [Tooltip("Pulls prisms and crystals in from (u round the way in)")]
        [SerializeField, Range(0f, 400f)] float suckRadius = 90f;
        [Tooltip("...at (u/s at that reach; faster as they near)")]
        [SerializeField, Range(0f, 300f)] float suckSpeed = 30f;
        [Tooltip("Swallows prisms within (× the opening radius)")]
        [SerializeField, Range(0f, 4f)] float swallowShare = 1.5f;
        [Tooltip("Inside, stretched along the tube by (+ × its length, at the middle)")]
        [SerializeField, Range(0f, 6f)] float carryStretch = 2f;
        [Tooltip("...and twisted (turns, mouth to mouth)")]
        [SerializeField, Range(0f, 4f)] float carryTwist = 0.5f;
        [Tooltip("Fires them out at (u/s)")]
        [SerializeField, Range(0f, 400f)] float fireSpeed = 120f;
        [Tooltip("...gliding to rest over (s)")]
        [SerializeField, Range(0.2f, 10f)] float fireGlideSeconds = 3f;
        [Tooltip("At most this many prisms in one Meerkat's pull, tube and glide at once (a performance cap, not in the " +
                 "artifact: the prism-motion ladder, Docs/PRISM_MOTION.md §3, drops to spheres past 256 movers)")]
        [SerializeField, Range(16, 1024)] int swallowPrismCap = 256;

        // ---- Garrett's kit (Garrett's design, approved by Yash 2026-10-28; R_VesselActions/MEERKAT.md section 10): the
        // artifact's Play Style Config > Kit, "Garrett's kit", by the page's own keys. Plain numbers are written by
        // studio_to_unity.py (Tools/Build/studio_to_unity/meerkat.json); each ElementalFloat is the page's L0 -> L10 pair
        // (rushL0/rushL10, zapPetalsL0/zapPetalsL10, portalSizeL0/portalSizeL10), authored once by author_meerkat_assets.py.
        // Read through MeerkatSolitonKit. Space (the surface area, the swallow and fire) is round 23's section above
        // (areaL0 / areaL10, suck*, swallowShare, carrySpeed, fireSpeed, fireGlideSeconds; MeerkatSwallow).
        [Header("Kit · Time, the ride (the artifact's Kit > Time)")]
        [Tooltip("The rush: the throat's speed ×, Time L0 -> ...at Time L10 (every soliton's ride, its own or anyone's)")]
        [SerializeField] ElementalFloat rush = ElementalFloat.Multiplier(1f, 1.4f, Element.Time, 0.05f);
        [Tooltip("Kit L5 Boost out: out of every soliton at (× the exit)")]
        [SerializeField, Range(1f, 3f)] float boostOut = 1.5f;

        [Header("Kit · Charge, Sentinel mode (the artifact's Kit > Sentinel)")]
        [Tooltip("Sentinels at once")]
        [SerializeField, Range(1, 6)] int sentinelPool = 3;
        [Tooltip("...at Charge L5")]
        [SerializeField, Range(1, 12)] int sentinelPoolL5 = 6;
        [Tooltip("Guard (× the opening radius)")]
        [SerializeField, Range(0.5f, 4f)] float guardShare = 1.5f;
        [Tooltip("An empty sentinel's AI zaps (a second)")]
        [SerializeField, Range(0f, 12f)] float aiZapRate = 5f;
        [Tooltip("A zap knocks out, Charge L0 (petal) -> ...at Charge L10 (petal), on each element, a sub-petal tick through the economy's accrual")]
        [SerializeField] ElementalFloat zapPetals = ElementalFloat.Multiplier(0.1f, 0.25f, Element.Charge, 0f);
        [Tooltip("Unity only: the seconds before a sentinel's zap can score on the same rival again (VesselCombatHitLatch; 0 = every zap is a scored hit)")]
        [SerializeField, Min(0f)] float zapCooldownSeconds;
        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager scores combat hits on. Fail-loud.")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("Kit · Mass, the exit portal (the artifact's Kit > Portal)")]
        [Tooltip("Exit portal: the hexagon out at (× the exit radius)")]
        [SerializeField, Range(1f, 4f)] float portalShare = 1.5f;
        [Tooltip("Its prisms' size, Mass L0 (× a prism) -> ...at Mass L10 (× the hull's trail prism)")]
        [SerializeField] ElementalFloat portalSize = ElementalFloat.Multiplier(0.4f, 0.9f, Element.Mass, 0.05f);

        [Header("Kit · Audio (FMOD; shipped empty until sound design wires them)")]
        [Tooltip("A sentinel zaps a rival (at the rival).")]
        [SerializeField] EventReference zapEvent;
        [Tooltip("A sentinel stands up at a new soliton's way in.")]
        [SerializeField] EventReference sentinelPlacedEvent;
        [Tooltip("A sentinel is lost or retires, and its soliton with it.")]
        [SerializeField] EventReference sentinelLostEvent;
        [Tooltip("A new soliton sets down its exit portal (at the way out).")]
        [SerializeField] EventReference portalEvent;
        [Tooltip("A soliton fires a prism out of its way out.")]
        [SerializeField] EventReference prismFiredEvent;

        [Header("Miscellaneous · audio (FMOD; shipped empty until sound design wires them)")]
        [Tooltip("RT formed a tunnel (at the hull).")]
        [SerializeField] EventReference formEvent;
        [Tooltip("A tunnel collapsed (at its centre).")]
        [SerializeField] EventReference collapseEvent;
        [Tooltip("An omnicrystal was taken and the roots grew (at the hull).")]
        [SerializeField] EventReference rootsEvent;

        [Header("Miscellaneous · the reticle (yours alone: its feel, its timing, its frame)")]
        [Tooltip("Your reticle follows the sticks over (s, 0 = at once)")]
        [SerializeField, Range(0f, 1f)] float reticleSmoothSeconds = 0.15f;
        [Tooltip("Your reticle, right stick centred (brightness)")]
        [SerializeField, Range(0f, 1f)] float reticleIdleAlpha = 0.4f;
        [Tooltip("Your reticle's colour: your domain colour toward white")]
        [SerializeField, Range(0f, 1f)] float reticleWhite = 0.45f;
        [Tooltip("...its brightness at the way in, against its far end (1 = even)")]
        [SerializeField, Range(0f, 1f)] float reticleNearLight = 0.25f;
        [Tooltip("The reticle comes back after you are out in (s)")]
        [SerializeField, Range(0f, 3f)] float reticleDelaySeconds = 0.5f;
        [Tooltip("...rippling into shape over (s)")]
        [SerializeField, Range(0.05f, 3f)] float reticleGrowSeconds = 0.8f;
        [Tooltip("The reticle's frame (the bridge's wireframe): rings of spindles along it")]
        [SerializeField, Range(2, 40)] int bridgeRings = 16;
        [Tooltip("...spindles round each ring")]
        [SerializeField, Range(3, 32)] int bridgeSpindlesPerRing = 12;
        [Tooltip("...ribs of spindles along it (0 = none)")]
        [SerializeField, Range(0, 16)] int bridgeRibs = 6;
        [Tooltip("...spindle thickness (u)")]
        [SerializeField, Range(0.1f, 6f)] float bridgeSpindleThickness = 1.6f;
        [Tooltip("...a shimmer running way in to way out")]
        [SerializeField, Range(0f, 1f)] float bridgeShimmer = 0.5f;

        [Header("Miscellaneous · AI (the artifact's AI tab)")]
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


        // The look (Comfort, Trunk / Root Look and Ride); read through MeerkatWormholeLookTuning (the lab's live copies)
        public MeerkatWormholeComfort Comfort => comfort;
        public MeerkatWormholeLook TrunkLook => trunkLook;
        public MeerkatWormholeLook RootLook => rootLook;
        public MeerkatWormholeRideView TrunkRide => trunkRide;
        public MeerkatWormholeRideView RootRide => rootRide;

        // Garrett's kit (MeerkatSolitonKit reads these)
        public ElementalFloat Rush => rush;
        public float BoostOut => boostOut;
        public int SentinelPool => sentinelPool;
        public int SentinelPoolL5 => sentinelPoolL5;
        public float GuardShare => guardShare;
        public float AiZapRate => aiZapRate;
        public ElementalFloat ZapPetals => zapPetals;
        public float ZapCooldownSeconds => zapCooldownSeconds;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public float PortalShare => portalShare;
        public ElementalFloat PortalSize => portalSize;
        public EventReference ZapEvent => zapEvent;
        public EventReference SentinelPlacedEvent => sentinelPlacedEvent;
        public EventReference SentinelLostEvent => sentinelLostEvent;
        public EventReference PortalEvent => portalEvent;
        public EventReference PrismFiredEvent => prismFiredEvent;

        public float ReticleAhead => reticleAhead;
        public float KeyRampSeconds => keyRampSeconds;
        public float RideExitMultiplier => rideExitMultiplier;
        public float RideSpeedTunnelScale => rideSpeedTunnelScale;
        public float ReticleDelaySeconds => reticleDelaySeconds;
        public float ReticleGrowSeconds => reticleGrowSeconds;
        public float FormSeconds => formSeconds;
        public float StaySeconds => staySeconds;
        public float CollapseSeconds => collapseSeconds;
        public float AreaL0 => areaL0;
        public float AreaL10 => areaL10;
        public float SuckRadius => suckRadius;
        public float SuckSpeed => suckSpeed;
        public float SwallowShare => swallowShare;
        public float CarryStretch => carryStretch;
        public float CarryTwist => carryTwist;
        public float FireSpeed => fireSpeed;
        public float FireGlideSeconds => fireGlideSeconds;
        public int SwallowPrismCap => swallowPrismCap;
        public float ReticleSmoothSeconds => reticleSmoothSeconds;
        public float ReticleIdleAlpha => reticleIdleAlpha;
        public float ReticleWhite => reticleWhite;
        public float ReticleNearLight => reticleNearLight;
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
        public EventReference RootsEvent => rootsEvent;
        public bool OmniOn => omniOn;
        public bool MenuAutopilotRoots => menuAutopilotRoots;
        public int OmniCount => omniCount;
        public int LabyrinthBranchPointsMin => labyrinthBranchPointsMin;
        public int LabyrinthBranchPointsMax => labyrinthBranchPointsMax;
        public int LabyrinthOpeningsMin => labyrinthOpeningsMin;
        public int LabyrinthOpeningsMax => labyrinthOpeningsMax;
        public float LabyrinthRadiusMin => labyrinthRadiusMin;
        public float LabyrinthRadiusMax => labyrinthRadiusMax;
        /// <summary>Round 26: the network's reach as a share of its cell's radius, on this hull's live Mass (at least 0.05).</summary>
        public float NetworkShare(IVesselStatus status) => Mathf.Max(0.05f, networkReach.EvaluateLive(status));
        public float LabyrinthSpread => labyrinthSpread;
        public float LabyrinthRandomness => labyrinthRandomness;
        public float LabyrinthDiameter => labyrinthDiameter;
        public float LabyrinthMouthDiameter => labyrinthMouthDiameter;
        public float RootTip => rootTip;
        public float RootGnarl => rootGnarl;
        public float LabyrinthFormSeconds => labyrinthFormSeconds;
        public float RootCruise => rootCruise;
        public float RootThrust => rootThrust;
        public float RootMove => rootMove;
        public float RootDrag => rootDrag;
        public float RootMaxSpeed => rootMaxSpeed;
        public float RootBounce => rootBounce;
        public float RootGravity => rootGravity;

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

        /// <summary>The shape rows. <paramref name="spaceLevel"/> (0-10, the hull's Space level) sets the soliton's surface
        /// area (round 23); without it the size is the authored throat and mouth.</summary>
        public MeerkatWormholeMath.ShapeSettings Shape(float spaceLevel = -1f) => new()
        {
            ShaftMin = shaftMin, ShaftMax = shaftMax, BendRadius = bendRadius, BendMaxDegrees = bendMaxDegrees,
            ThroatRadius = 0.5f * throatDiameter, MouthRadius = 0.5f * mouthDiameter,   // round 10: authored as diameters
            OvalShortest = ovalShortest, OvalLongest = ovalLongest, OvalMaxSharpness = ovalMaxSharpness,   // round 22: the oval arc
            Area = spaceLevel < 0f ? 0f : MeerkatWormholeMath.ByLevel(areaL0, areaL10, spaceLevel),     // round 23: Garrett's area
        };

        /// <summary>The AI's plan rows; <paramref name="turnRateDegrees"/> is the hull's own turn (its transformer's YawScaler,
        /// the studio's pitchYaw).</summary>
        public MeerkatWormholeMath.PlanSettings Plan(float turnRateDegrees, float spaceLevel = -1f) => new()
        {
            ReticleAhead = reticleAhead, ShapeSeconds = aiShapeSeconds, Shape = Shape(spaceLevel),
            SlideSpeed = rootMaxSpeed,   // round 24: the AI slides with RT held
            TurnRateRadians = turnRateDegrees * Mathf.Deg2Rad,
        };
    }
}
