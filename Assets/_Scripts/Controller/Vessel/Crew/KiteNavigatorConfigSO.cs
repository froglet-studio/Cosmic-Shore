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
        [Tooltip("lab reachY: anything within this of the plane (u) can be touched; further away it is drawn as a hollow ghost.")]
        [SerializeField, Min(1f)] float reachY = 40f;

        [Header("Kite")]
        [Tooltip("lab kiteRadius: how near a prism or a rival hull the kite's head must pass (u).")]
        [SerializeField, Min(1f)] float kiteRadius = 8f;
        [Tooltip("lab kiteSpeed: the kite's top speed toward the aim point (u/s).")]
        [SerializeField, Min(10f)] float kiteSpeed = 320f;
        [Tooltip("lab kiteAccel: how fast the kite reaches the speed it is asked for (1/s).")]
        [SerializeField, Min(0.1f)] float kiteAccel = 12f;
        [Tooltip("lab tailLen: the tail's length in board radii. Cross it to close a loop. 1.0 is too short to circle a cluster.")]
        [SerializeField, Range(0.4f, 4f)] float tailLength = 1.6f;
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
        [Tooltip("lab timeBoost: seconds of boost one Time mote gives the pilot. The pilot spends it by lifting the thumb.")]
        [SerializeField, Min(0.05f)] float timeBoostSeconds = 1.5f;
        [Tooltip("lab timeCap: the most boost the pilot can hold from the navigator (s).")]
        [SerializeField, Min(0.5f)] float timeBoostCap = 30f;
        [Tooltip("Seconds after a boost ends before the next can start. Time L5 removes the wait.")]
        [SerializeField, Min(0f)] float boostCooldownSeconds = 2f;
        [Tooltip("lab chargePetals: kite power one Charge mote gives: petals the next ram on a rival hull steals.")]
        [SerializeField, Min(0.05f)] float chargePetals = 1f;
        [Tooltip("lab chargeCap: the most kite power held.")]
        [SerializeField, Min(1f)] float chargeCap = 30f;
        [Tooltip("lab stealCooldown: seconds between two rams on the same rival.")]
        [SerializeField, Min(0f)] float stealCooldown = 1f;
        [Tooltip("Raised for every ram that steals, as every combat hit is (the scoreboard's channel; the Thresher raises the same one).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("AI squad mate (flies the kite while you fly the hull)")]
        [Tooltip("The navigator AI's skill: thumb speed, reaction, aim noise and judgement (never the kite itself).")]
        [SerializeField] KiteNavigatorAI.Skill aiSkill = KiteNavigatorAI.Skill.Hard;

        [Header("Omni crystal: the wide board")]
        [Tooltip("Seconds an Omni crystal the hull collects widens the kite's board.")]
        [SerializeField, Min(0f)] float omniSeconds = 5f;
        [Tooltip("How much wider: the kite view zooms out this many times, the kite may fly that much further, and the " +
                 "lasso (measured in board radii) grows with it.")]
        [SerializeField, Range(1f, 4f)] float omniBoardScale = 2f;
        [Tooltip("How fast the board widens and narrows again (1/s).")]
        [SerializeField, Min(0.1f)] float omniZoomRate = 3f;

        [Header("Views (the game's renderer)")]
        [Tooltip("The small view's height, as a fraction of the screen: the kite view while piloting (a square), the pilot's " +
                 "view while navigating. Both sit in the lower left.")]
        [SerializeField, Range(0.15f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's gap from the screen's edges, as a fraction of the screen height.")]
        [SerializeField, Range(0f, 0.1f)] float viewMargin = 0.015f;
        [Tooltip("The petal shape each element's motes are drawn in, lime, in the kite view: Charge, Mass, Space, Time. An " +
                 "empty slot draws a plain petal.")]
        [SerializeField] Mesh[] petalMeshes = new Mesh[4];
        [Tooltip("A mote's size in the kite view (u).")]
        [SerializeField, Min(0.5f)] float moteSize = 3.2f;
        [Tooltip("The tail's width in the kite view (u).")]
        [SerializeField, Min(0.2f)] float tailWidth = 1.4f;
        [Tooltip("How visible the kite is to the pilot: a faint, see-through kite (0 = hidden).")]
        [SerializeField, Range(0f, 1f)] float pilotKiteAlpha = 0.22f;
        [Tooltip("How visible the motes are to the pilot: faint lime dots (0 = hidden).")]
        [SerializeField, Range(0f, 1f)] float pilotDotAlpha = 0.35f;
        [Tooltip("The size of a mote's dot in the pilot's view (u).")]
        [SerializeField, Min(0.1f)] float pilotDotSize = 0.9f;

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
        [Tooltip("A charged ram steals petals.")]
        [SerializeField] EventReference stealSfx;

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
        public float OmniSeconds => omniSeconds;
        public float OmniBoardScale => omniBoardScale;
        public float OmniZoomRate => omniZoomRate;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public float MoteSize => moteSize;
        public float TailWidth => tailWidth;
        public float PilotKiteAlpha => pilotKiteAlpha;
        public float PilotDotAlpha => pilotDotAlpha;
        public float PilotDotSize => pilotDotSize;

        /// <summary>The petal mesh for a mote of element index <paramref name="element"/> (0 Charge .. 3 Time), or null.</summary>
        public Mesh PetalMesh(int element) =>
            petalMeshes != null && element >= 0 && element < petalMeshes.Length ? petalMeshes[element] : null;
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

        /// <summary>The kite core's numbers.</summary>
        public KiteSettings ToSettings() => new()
        {
            BoardRadius = boardRadius, Anchor = anchor, KiteSpeed = kiteSpeed, KiteAccel = kiteAccel, TailLength = tailLength,
        };
    }
}
