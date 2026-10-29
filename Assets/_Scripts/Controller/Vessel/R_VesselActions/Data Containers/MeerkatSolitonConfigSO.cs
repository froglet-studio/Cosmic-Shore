using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Meerkat's one ability, the SOLITON (<c>R_VesselActions/MEERKAT.md</c>): RT forms one Soliton
    /// Tunnel, an omnicrystal grows a Soliton Network. Cloned from the Vessel Studio
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>, its <c>SHIPPED</c> block and <c>SPEC</c> rows): every plain field is
    /// the page's key by the same name, its <c>[Tooltip]</c> is the page's label word for word, and its default is the
    /// page's shipped value. <c>Tools/Build/studio_to_unity/meerkat.json</c> maps each key here (or on the tunnel configs);
    /// the artifact is the one source of truth for those, so tune it there and clone (<c>/artifact-to-unity</c>).
    ///
    /// <para><b>The tubes themselves are not here</b> (Yash, 2026-10-29: Soliton Tunnels are an environmental fundamental,
    /// not Meerkat-only). Their look, comfort, ride view, bridge, tint, size, life and the slip-and-slide are on two
    /// <see cref="SolitonTunnelConfigSO"/> assets this one points to: <see cref="TunnelConfig"/> (what RT forms) and
    /// <see cref="NetworkConfig"/> (what an omnicrystal grows; the tunnel's when empty). Edit them in the inspector or in
    /// FrogletTools > Environment > Environmental Fundamentals. What stays here is the Meerkat's own: the reticle and how
    /// RT and LT shape a tunnel (shaft, bend, Space's area), the omnicrystal, Garrett's kit, the AI and the audio.</para>
    ///
    /// <para>Moved 2026-10-29 to <see cref="SolitonTunnelConfigSO"/> (<c>Docs/Shaders/WORMHOLE_SHADERS.md</c> lists them and
    /// the commit to restore from, 2d1e8726c): <c>comfort</c>, <c>trunkLook</c> / <c>rootLook</c>, <c>trunkRide</c> /
    /// <c>rootRide</c>, <c>bendRadius</c>, <c>oval*</c>, <c>throatDiameter</c>, <c>mouthDiameter</c>, <c>formSeconds</c>,
    /// <c>staySeconds</c>, <c>collapseSeconds</c>, <c>bridge*</c>, <c>labyrinth*</c> (now <c>network*</c>),
    /// <c>rootTip</c> / <c>rootGnarl</c>, <c>rideExitMultiplier</c>, <c>rideSpeedTunnelScale</c>, <c>rootMaxSpeed</c> and
    /// <c>rootBounce</c>. Retired with the old steered flight: <c>rootCruise</c>, <c>rootThrust</c>, <c>rootMove</c>,
    /// <c>rootDrag</c>, <c>rootGravity</c> (the slide's wall gravity, small RT thrust and speed floor replace them).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "MeerkatSolitonConfig", menuName = "ScriptableObjects/Vessel Actions/Meerkat Soliton Config")]
    public sealed class MeerkatSolitonConfigSO : ScriptableObject
    {
        [Header("Soliton Tunnels (the tubes themselves: look, tint, size, life, the slip-and-slide)")]
        [Tooltip("What RT forms: a single Soliton Tunnel's config (an environmental fundamental, shared with any vessel)")]
        [SerializeField] SolitonTunnelConfigSO tunnelConfig;
        [Tooltip("What an omnicrystal grows: a Soliton Network's config (empty: the tunnel's)")]
        [SerializeField] SolitonTunnelConfigSO networkConfig;

        [Header("Tunnel Shape (how RT and LT shape the tunnel you form: the artifact's Wormhole shape)")]
        [Tooltip("The reticle rides this far ahead of the nose (u)")]
        [SerializeField, Range(0f, 100f)] float reticleAhead = 30f;
        [Tooltip("Sharpest bend, the right stick fully over (°)")]
        [SerializeField, Range(0f, 180f)] float bendMaxDegrees = 150f;
        [Tooltip("The straight shaft before the bend, LT released (u)")]
        [SerializeField, Range(0f, 600f)] float shaftMin = 60f;
        [Tooltip("...LT fully squeezed (u)")]
        [SerializeField, Range(20f, 1500f)] float shaftMax = 600f;
        [Tooltip("LT on a key or touch: full after (s)")]
        [SerializeField, Range(0.05f, 3f)] float keyRampSeconds = 0.5f;

        [Header("Soliton Network (an omnicrystal's: the artifact's Omnicrystal; the network's shape is on its config)")]
        [Tooltip("The Omnicrystal ability: an omnicrystal opens the wormhole network (0 = off)")]
        [SerializeField] bool omniOn = true;
        [Tooltip("Perf (2026-10-29): the menu's autopilot Meerkat (the lava lamp behind Menu_Main) grows Soliton Networks from " +
                 "omnicrystals too. Off: it takes none, so the lava lamp keeps its frame rate; a person flying in freestyle, " +
                 "and every match, grow them as ever")]
        [SerializeField, FormerlySerializedAs("menuAutopilotRoots")] bool menuAutopilotNetworks = false;
        [Tooltip("Round 26 (Yash, 2026-10-28: \"it will always be large ... if it has full mass, then it will be the largest, " +
                 "covering the entire cell\"): the network's reach as a share of its cell's radius (MeerkatNetworkCell), on Mass - " +
                 "Min the page's networkReachAtZero (0.5, at Mass 0), Max its networkReachAtFull (1, the whole cell, at Mass 10). " +
                 "Read when the omnicrystal is taken (EvaluateLive). An ElementalFloat block, so studio_to_unity.py cannot write it: " +
                 "keep it equal to the page's two keys by hand.")]
        [SerializeField] ElementalFloat networkReach = ElementalFloat.Multiplier(0.5f, 1f, Element.Mass, 0.05f);
        [Tooltip("Omnicrystals on the course (a new course or reset places them)")]
        [SerializeField, Range(0, 12)] int omniCount = 3;

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
        [Tooltip("An omnicrystal was taken and a Soliton Network grew (at the hull).")]
        [SerializeField, FormerlySerializedAs("rootsEvent")] EventReference networkEvent;

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


        /// <summary>A single tunnel's config (what RT forms).</summary>
        public SolitonTunnelConfigSO TunnelConfig => tunnelConfig;
        /// <summary>A network's config (what an omnicrystal grows; the tunnel's when empty).</summary>
        public SolitonTunnelConfigSO NetworkConfig => networkConfig ? networkConfig : tunnelConfig;

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
        public float RideExitMultiplier => tunnelConfig ? tunnelConfig.ExitMultiplier : 1.2f;
        public float RideSpeedTunnelScale => tunnelConfig ? tunnelConfig.RideSpeedTunnelScale : 5f;
        public float ReticleDelaySeconds => reticleDelaySeconds;
        public float ReticleGrowSeconds => reticleGrowSeconds;
        public float FormSeconds => tunnelConfig ? tunnelConfig.FormSeconds : 0.4f;
        public float StaySeconds => tunnelConfig ? tunnelConfig.StaySeconds : 10f;
        public float CollapseSeconds => tunnelConfig ? tunnelConfig.CollapseSeconds : 0.8f;
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
        public bool AiWormhole => aiWormhole;
        public float AiStraightMinDistance => aiStraightMinDistance;
        public float AiBentMinDistance => aiBentMinDistance;
        public float AiShapeSeconds => aiShapeSeconds;
        public float AiNoise => aiNoise;
        public EventReference FormEvent => formEvent;
        public EventReference CollapseEvent => collapseEvent;
        public EventReference NetworkEvent => networkEvent;
        public bool OmniOn => omniOn;
        public bool MenuAutopilotNetworks => menuAutopilotNetworks;
        public int OmniCount => omniCount;
        /// <summary>Round 26: the network's reach as a share of its cell's radius, on this hull's live Mass (at least 0.05).</summary>
        public float NetworkShare(IVesselStatus status) => Mathf.Max(0.05f, networkReach.EvaluateLive(status));
        /// <summary>The slide's top speed in a tunnel (u/s; the AI's plan reads it).</summary>
        public float SlideMaxSpeed => tunnelConfig ? tunnelConfig.MaxSpeed : 119f;

        /// <summary>The shape rows. <paramref name="spaceLevel"/> (0-10, the hull's Space level) sets the soliton's surface
        /// area (round 23); without it the size is the authored throat and mouth.</summary>
        public SolitonTunnelShapeMath.ShapeSettings Shape(float spaceLevel = -1f)
        {
            float area = spaceLevel < 0f ? 0f : SolitonTunnelShapeMath.ByLevel(areaL0, areaL10, spaceLevel);   // round 23: Garrett's area
            if (tunnelConfig) return tunnelConfig.Shape(shaftMin, shaftMax, bendMaxDegrees, area);
            return new SolitonTunnelShapeMath.ShapeSettings
            {
                ShaftMin = shaftMin, ShaftMax = shaftMax, BendRadius = 10f, BendMaxDegrees = bendMaxDegrees,
                ThroatRadius = 3.55f, MouthRadius = 17.8f, OvalShortest = 0.2f, OvalLongest = 1.1f, OvalMaxSharpness = 8f, Area = area,
            };
        }

        /// <summary>The AI's plan rows; <paramref name="turnRateDegrees"/> is the hull's own turn (its transformer's YawScaler,
        /// the studio's pitchYaw).</summary>
        public SolitonTunnelShapeMath.PlanSettings Plan(float turnRateDegrees, float spaceLevel = -1f) => new()
        {
            ReticleAhead = reticleAhead, ShapeSeconds = aiShapeSeconds, Shape = Shape(spaceLevel),
            SlideSpeed = SlideMaxSpeed,   // the AI slides with RT held
            TurnRateRadians = turnRateDegrees * Mathf.Deg2Rad,
        };
    }
}
