using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How the navigator's board follows the hull it rides on.</summary>
    public enum KiteBoardFrame
    {
        /// <summary>The board eases toward the hull's x-z plane (the signed-off frame).</summary>
        Eased = 0,
        /// <summary>The board IS the hull's x-z plane every frame.</summary>
        Locked = 1,
        /// <summary>The board stays level and turns only with heading (a comparison, not the design).</summary>
        Level = 2,
    }

    /// <summary>Where the navigator's motes live.</summary>
    public enum KiteMoteHome
    {
        /// <summary>Motes flow past with the world but always stay in the board's plane (the signed-off home).</summary>
        Board = 0,
        /// <summary>Motes are fixed in space: only what the pilot sweeps the plane through can be looped.</summary>
        World = 1,
    }

    /// <summary>
    /// Every number of the navigator's kite (Crew Vessel Lab round 3, the "Kite &amp; Tail" candidate Garrett signed off on
    /// 2026-10-17; <c>Docs/Studios/CREW_VESSEL_LAB.md</c>). The lab is the record: each field names the lab key it came from,
    /// and <c>KiteNavigatorConfigTests</c> holds the shipped asset to the lab's SHIPPED block. Read by
    /// <see cref="KiteNavigator"/>; see <c>Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "KiteNavigatorConfig", menuName = "ScriptableObjects/Vessel Actions/Kite Navigator Config")]
    public sealed class KiteNavigatorConfigSO : ScriptableObject
    {
        [Header("Board (the hull's x-z plane)")]
        [Tooltip("lab planeRange: how far of the hull's plane the board shows (u).")]
        [SerializeField, Min(10f)] float boardRadius = 160f;
        [Tooltip("lab anchor: the board's centre sits this many board radii AHEAD of the hull, so more of it is ahead.")]
        [SerializeField, Range(0f, 0.7f)] float anchor = 0.3f;
        [Tooltip("How the board follows the hull. Eased is the signed-off frame.")]
        [SerializeField] KiteBoardFrame frame = KiteBoardFrame.Eased;
        [Tooltip("lab easeRate: Eased frame, how fast the board follows a bank or pitch (1/s).")]
        [SerializeField, Min(0.1f)] float easeRate = 3f;
        [Tooltip("lab reachY: anything within this of the plane (u) can be touched; further away it is out of reach (still seen).")]
        [SerializeField, Min(1f)] float reachY = 40f;

        [Header("Kite")]
        [Tooltip("lab kiteRadius: how near a prism or a rival hull the kite's head must pass (u).")]
        [SerializeField, Min(1f)] float kiteRadius = 8f;
        [Tooltip("lab kiteSpeed: the kite's top speed toward the aim point (u/s).")]
        [SerializeField, Min(10f)] float kiteSpeed = 320f;
        [Tooltip("lab kiteAccel: how fast the kite reaches the speed it is asked for (1/s).")]
        [SerializeField, Min(0.1f)] float kiteAccel = 12f;
        [Tooltip("lab tailLen: the tail's length in board radii. Cross it to close a loop. 1.0 is too short to circle a cluster. " +
                 "2.4 = the signed-off 1.6 + 50% (Garrett, 2026-10-18).")]
        [SerializeField, Range(0.4f, 4f)] float tailLength = 2.4f;
        [Tooltip("lab thumbOffset (80 CSS px on a phone): on a touch screen the kite aims this far ABOVE the thumb, in millimetres " +
                 "of glass, so the thumb never covers the kite or what is coming.")]
        [SerializeField, Range(0f, 40f)] float thumbOffsetMillimetres = 21f;
        [Tooltip("Lift the aim above a mouse pointer too, to feel the touch offset on a PC.")]
        [SerializeField] bool offsetMouse;
        [Tooltip("A thumbstick leaning less than this is centred: the kite hovers. The kite follows the stick's lean, the " +
                 "mouse or the thumb; nothing is clicked.")]
        [SerializeField, Range(0f, 0.5f)] float stickDeadzone = 0.15f;

        [Header("Motes (navigator only)")]
        [Tooltip("Where the motes live. Board is the signed-off home.")]
        [SerializeField] KiteMoteHome moteHome = KiteMoteHome.Board;
        [Tooltip("lab moteCount: motes kept near the board. Only the navigator's screen draws them.")]
        [SerializeField, Range(4, 80)] int moteCount = 26;
        [Tooltip("lab moteCluster: average motes per same-element cluster. With growth 2, this and the spread set how big loops get.")]
        [SerializeField, Range(1, 10)] int moteCluster = 4;
        [Tooltip("lab cluster spread (u, gaussian). A balance knob as strong as the growth rate: 14 -> 18 cut the lab's Space blasts 27x (L-STU-84).")]
        [SerializeField, Range(4f, 40f)] float moteSpread = 18f;
        [Tooltip("What a loop of more than one element does. Fizzle is the signed-off rule.")]
        [SerializeField] KiteMixedLoopRule mixedLoops = KiteMixedLoopRule.Fizzle;
        [Tooltip("How often each element comes to the kite is weighted base + perLevel x the hull's level in it, so the " +
                 "pilot's petals steer what the navigator can loop. This is the weight an element has at level 0.")]
        [SerializeField, Min(0f)] float moteBaseWeight = 1f;
        [Tooltip("The weight each petal (level) of an element on the hull adds to how often that element comes.")]
        [SerializeField, Min(0f)] float moteWeightPerLevel = 1f;

        [Header("Effects: base x growth^(n - 1) for a pure loop of n motes")]
        [Tooltip("lab growth: each extra mote in a pure loop multiplies the effect by this.")]
        [SerializeField, Range(1f, 4f)] float growth = 2f;
        [Tooltip("SPACE: the AOEExplosion prefab(s) a Space loop detonates at its centre, sparing our own domain.")]
        [SerializeField] AOEExplosion[] spaceExplosions;
        [Tooltip("lab spaceScale: AOEExplosion MaxScale for one Space mote (the blast's radius is half of it).")]
        [SerializeField, Min(1f)] float spaceMaxScale = 60f;
        [Tooltip("lab spaceCap: the largest MaxScale a Space blast may reach.")]
        [SerializeField, Min(1f)] float spaceMaxScaleCap = 640f;
        [Tooltip("lab massPrisms: prisms in the cluster one Mass mote lays, in our domain.")]
        [SerializeField, Min(1f)] float massPrisms = 6f;
        [Tooltip("lab massCap: most prisms one Mass cluster may hold.")]
        [SerializeField, Min(1f)] float massPrismCap = 240f;
        [Tooltip("The size of each Mass prism (the Sparrow's trail prism, VesselPrismController.BaseScale).")]
        [SerializeField] Vector3 massPrismScale = new(2f, 2f, 5f);
        [Tooltip("Mass prisms laid per frame at most, so a big cluster grows in over a few frames instead of in one hitch.")]
        [SerializeField, Range(4, 200)] int massPrismsPerFrame = 40;
        [Tooltip("lab timeBoost: one saved boost lasts this long (s). A Time loop of n motes saves growth^(n-1) of them, the " +
                 "lab's timeBoost x growth^(n-1) seconds in whole boosts, spent one at a time on the pilot's trigger.")]
        [SerializeField, Min(0.05f)] float timeBoostSeconds = 1.5f;
        [Tooltip("lab timeCap: the most boost the pilot can hold from the navigator (s): timeCap / timeBoost saved boosts.")]
        [SerializeField, Min(0.5f)] float timeBoostCap = 30f;
        [Tooltip("Seconds after a boost ends before the next can start. Time L5 removes the wait. The pilot boosts on the " +
                 "right trigger (the left mouse button with the one-stick mouse; lifting the thumb on a touch screen).")]
        [SerializeField, Min(0f)] float boostCooldownSeconds = 2f;
        [Tooltip("lab chargePetals: kite power one Charge mote gives: petals the next ram on a rival hull steals.")]
        [SerializeField, Min(0.05f)] float chargePetals = 1f;
        [Tooltip("lab chargeCap: the most kite power held.")]
        [SerializeField, Min(1f)] float chargeCap = 30f;
        [Tooltip("lab stealCooldown: seconds between two rams on the same rival.")]
        [SerializeField, Min(0f)] float stealCooldown = 1f;
        [Tooltip("Kite power a charged ram spends on each creature it kills.")]
        [SerializeField, Min(0f)] float faunaChargeCost = 1f;
        [Tooltip("Raised for every ram that steals, as every combat hit is (the scoreboard's channel; the Thresher raises the same one).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("AI squad mate (flies the kite while you fly the hull)")]
        [Tooltip("The navigator AI's skill: thumb speed, reaction, aim noise and judgement (never the kite itself).")]
        [SerializeField] KiteNavigatorAI.Skill aiSkill = KiteNavigatorAI.Skill.Hard;

        [Header("Omni crystal: a burst of motes (Garrett, round 4e)")]
        [Tooltip("An Omni crystal the hull collects drops one cluster of each element on the kite's board, this many motes " +
                 "each, so a crew short of crystals can make it up (round 4e: \"if they are low on elemental crystals the " +
                 "omnicrystal can inject a bunch to make up for low production\").")]
        [SerializeField, Range(0, 24)] int omniMotesPerElement = 6;
        [Tooltip("Where the four clusters land: this far from the board's centre, as a fraction of its radius, evenly round it.")]
        [SerializeField, Range(0f, 0.9f)] float omniClusterRadius = 0.5f;

        [Header("Views (the game's renderer)")]
        [Tooltip("The small view's height, as a fraction of the screen: a square in the lower left, the kite view while " +
                 "piloting and the pilot's view while navigating.")]
        [SerializeField, Range(0.15f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's gap from the screen's edges, as a fraction of the screen height.")]
        [SerializeField, Range(0f, 0.1f)] float viewMargin = 0.015f;
        [Tooltip("A mote's flower radius in the kite view (u). The petals sit in it in the totem's own proportions (the " +
                 "element flower above each ability card).")]
        [SerializeField, Min(0.5f)] float moteSize = 4.5f;

        [Header("Mote flowers (five petals, each moving like its element; rounds 4e-4f)")]
        [Tooltip("Space: the flower spins this many degrees a second, without pausing.")]
        [SerializeField, Min(0f)] float spaceSpinDegreesPerSecond = 150f;
        [Tooltip("Mass: how far a petal moves out, as a fraction of the flower's radius.")]
        [SerializeField, Range(0f, 2f)] float massPulse = 0.72f;
        [Tooltip("Mass: how many of the five petals are out at once; the rest are held in (round 4f: three held in).")]
        [SerializeField, Range(1, 4)] int massPetalsOut = 2;
        [Tooltip("Mass: seconds each set of petals is out before the next set takes over.")]
        [SerializeField, Min(0.05f)] float massStepSeconds = 0.5f;
        [Tooltip("Charge: seconds the one plasma bolt takes to lap all five petals.")]
        [SerializeField, Min(0.1f)] float chargeBoltLapSeconds = 1.6f;
        [Tooltip("Charge: the bolt's head, as a fraction of the flower's radius.")]
        [SerializeField, Range(0.05f, 1f)] float chargeSparkSize = 0.22f;
        [Tooltip("Charge: how many fading sparks trail the bolt's head.")]
        [SerializeField, Range(0, 16)] int chargeBoltTrail = 7;

        [Tooltip("The tail's width in the kite view (u).")]
        [SerializeField, Min(0.2f)] float tailWidth = 1.4f;
        [Tooltip("How visible the kite is to the pilot: a faint, see-through kite (0 = hidden).")]
        [SerializeField, Range(0f, 1f)] float pilotKiteAlpha = 0.22f;
        [Tooltip("How visible the motes are to the pilot: faint lime dots (0 = hidden).")]
        [SerializeField, Range(0f, 1f)] float pilotDotAlpha = 0.35f;
        [Tooltip("The size of a mote's dot in the pilot's view (u).")]
        [SerializeField, Min(0.1f)] float pilotDotSize = 0.9f;
        [Header("Kite view depth (Garrett, round 4d: out-of-plane prisms never disappear)")]
        [Tooltip("How far above AND below the plane the kite view sees (u). Its camera sits this high over the plane.")]
        [SerializeField, Min(1f)] float viewDepth = 400f;
        [Tooltip("The kite view reads prism depth as distance OUT OF THE PLANE, through the prisms' own depth look (their " +
                 "faces part with distance). A prism this far from the plane (u) reads as far as the pilot view's most " +
                 "distant mass; one on the plane reads near.")]
        [SerializeField, Min(1f)] float depthFullSpreadAt = 60f;
        [Tooltip("The prisms ABOVE the plane, between the kite view's lens and the board, dither in the occlusion " +
                 "corridor's cone so the board stays visible: their opacity in the cone's core (0 = cleared).")]
        [SerializeField, Range(0f, 1f)] float ditherCoreAlpha = 0.35f;
        [Tooltip("The cone's radius at the lens, as a fraction of its radius at the plane (1 = a cylinder).")]
        [SerializeField, Range(0f, 1f)] float ditherNearScale = 0.5f;

        [Header("Game")]
        [Tooltip("The runtime game data: its player list is how the board finds the rival hulls.")]
        [SerializeField] GameDataSO gameData;

        [Header("Audio (empty = silent; assign in FMOD's browser)")]
        [Tooltip("A loop closes with motes inside (any element).")]
        [SerializeField] EventReference loopClosedSfx;
        [Tooltip("A mixed loop fizzles.")]
        [SerializeField] EventReference loopFizzledSfx;
        [Tooltip("The kite's head pops a hostile prism.")]
        [SerializeField] EventReference prismPoppedSfx;
        [Tooltip("A Mass loop lays its cluster.")]
        [SerializeField] EventReference massClusterSfx;
        [Tooltip("A Time loop hands the pilot boost.")]
        [SerializeField] EventReference timeBoostSfx;
        [Tooltip("A Charge loop powers the kite.")]
        [SerializeField] EventReference chargeSfx;
        [Tooltip("A charged ram knocks out (or, at Charge L5, steals) petals.")]
        [SerializeField] EventReference stealSfx;
        [Tooltip("A charged ram kills a creature.")]
        [SerializeField] EventReference faunaStruckSfx;

        public float BoardRadius => boardRadius;
        public float Anchor => anchor;
        public KiteBoardFrame Frame => frame;
        public float EaseRate => easeRate;
        public float ReachY => reachY;
        public float KiteRadius => kiteRadius;
        public float ThumbOffsetMillimetres => thumbOffsetMillimetres;
        public bool OffsetMouse => offsetMouse;
        public float StickDeadzone => stickDeadzone;
        public float MoteBaseWeight => moteBaseWeight;
        public float MoteWeightPerLevel => moteWeightPerLevel;
        public float BoostCooldownSeconds => boostCooldownSeconds;
        public int OmniMotesPerElement => omniMotesPerElement;
        public float OmniClusterRadius => omniClusterRadius;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public float MoteSize => moteSize;
        public float SpaceSpinDegreesPerSecond => spaceSpinDegreesPerSecond;
        public float MassPulse => massPulse;
        public int MassPetalsOut => massPetalsOut;
        public float MassStepSeconds => massStepSeconds;
        public float ChargeBoltLapSeconds => chargeBoltLapSeconds;
        public float ChargeSparkSize => chargeSparkSize;
        public int ChargeBoltTrail => chargeBoltTrail;
        public float TailWidth => tailWidth;
        public float PilotKiteAlpha => pilotKiteAlpha;
        public float PilotDotAlpha => pilotDotAlpha;
        public float PilotDotSize => pilotDotSize;
        public float ViewDepth => viewDepth;
        public float DepthFullSpreadAt => depthFullSpreadAt;
        public float DitherCoreAlpha => ditherCoreAlpha;
        public float DitherNearScale => ditherNearScale;
        public KiteMoteHome MoteHome => moteHome;
        public int MoteCount => moteCount;
        public int MoteCluster => moteCluster;
        public float MoteSpread => moteSpread;
        public KiteMixedLoopRule MixedLoops => mixedLoops;
        public float Growth => growth;
        public AOEExplosion[] SpaceExplosions => spaceExplosions;
        public float SpaceMaxScale => spaceMaxScale;
        public float SpaceMaxScaleCap => spaceMaxScaleCap;
        public float MassPrisms => massPrisms;
        public float MassPrismCap => massPrismCap;
        public Vector3 MassPrismScale => massPrismScale;
        public int MassPrismsPerFrame => massPrismsPerFrame;
        public float TimeBoostSeconds => timeBoostSeconds;
        public float TimeBoostCap => timeBoostCap;
        public float ChargePetals => chargePetals;
        public float ChargeCap => chargeCap;
        public float StealCooldown => stealCooldown;
        public float FaunaChargeCost => faunaChargeCost;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public KiteNavigatorAI.Skill AISkill => aiSkill;
        public GameDataSO GameData => gameData;
        public EventReference LoopClosedSfx => loopClosedSfx;
        public EventReference LoopFizzledSfx => loopFizzledSfx;
        public EventReference PrismPoppedSfx => prismPoppedSfx;
        public EventReference MassClusterSfx => massClusterSfx;
        public EventReference TimeBoostSfx => timeBoostSfx;
        public EventReference ChargeSfx => chargeSfx;
        public EventReference StealSfx => stealSfx;
        public EventReference FaunaStruckSfx => faunaStruckSfx;

        /// <summary>The kite core's numbers.</summary>
        public KiteSettings ToSettings() => new()
        {
            BoardRadius = boardRadius, Anchor = anchor, KiteSpeed = kiteSpeed, KiteAccel = kiteAccel, TailLength = tailLength,
        };
    }
}
