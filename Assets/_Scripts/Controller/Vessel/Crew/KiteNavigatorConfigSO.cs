using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;

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
        /// <summary>The board tilts with the hull's bank and pitch (as Eased), but turns to the hull's HEADING only as the
        /// hull travels: a yaw in place turns the Grouper on the screen and leaves the board, its motes and the kite where
        /// they are; over a medium distance the view comes round behind it (Garrett, 2026-10-20).</summary>
        Heading = 3,
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
        [Tooltip("How the board follows the hull. Heading (2026-10-20): it tilts with the hull but turns to its heading only " +
                 "as the hull travels, so a yaw turns the Grouper in place instead of dragging the motes round. Eased was " +
                 "the signed-off frame before it.")]
        [SerializeField] KiteBoardFrame frame = KiteBoardFrame.Heading;
        [Tooltip("Heading frame: the board turns to the hull's heading by 63% (one e-fold) for every this many units the hull " +
                 "flies (Garrett: \"slowly move toward the groupers so it aligns after medium distances\"). Standing still or " +
                 "turning in place never turns it. The kite keeps its place on the board, so the turn never moves it on screen.")]
        [SerializeField, Min(1f)] float headingAlignDistance = 220f;
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
                 "2.4 = the signed-off 1.6 + 50% (Garrett, 2026-10-18); 4.8 = doubled (Garrett, 2026-10-20: \"double the max " +
                 "length of the lasso\"). One lap must fit in it: a loop is at most tail / 2 pi across the radius.")]
        [SerializeField, Range(0.4f, 8f)] float tailLength = 4.8f;
        [Tooltip("lab thumbOffset (80 CSS px on a phone): on a touch screen the kite aims this far ABOVE the thumb, in millimetres " +
                 "of glass, so the thumb never covers the kite or what is coming.")]
        [SerializeField, Range(0f, 40f)] float thumbOffsetMillimetres = 21f;
        [Tooltip("Lift the aim above a mouse pointer too, to feel the touch offset on a PC.")]
        [SerializeField] bool offsetMouse;
        [Tooltip("A thumbstick leaning less than this is centred: the kite hovers. The kite follows the stick's lean, the " +
                 "mouse or the thumb; nothing is clicked.")]
        [SerializeField, Range(0f, 0.5f)] float stickDeadzone = 0.15f;

        [Header("Sling: the thumb holds the kite on an invisible rope (Wrasse Lasso Lab round 1)")]
        [Tooltip("How EVERY input holds the kite - a thumb, the mouse, a thumbstick and the AI squad mate alike (Garrett, " +
                 "2026-10-20: \"the gamepad and AI should be consistent with mouse and touch\"). Sling (\"Sling is excellent. " +
                 "bring it to unity\"): the input is a hand on a rope; drag and the kite trails, small circles whirl it round in " +
                 "big ones, lift the thumb to let it fly (a mouse and a stick never let go). Follow: the kite flies to a point " +
                 "above the thumb, the cursor, or the way the stick leans (round 3).")]
        [FormerlySerializedAs("pointerControl")]
        [SerializeField] KiteControl control = KiteControl.Sling;
        [Tooltip("lab slRope: the rope's length (u). One lap must fit in the tail (2 pi x rope < tail length x board radius), " +
                 "so above ~55 u no orbit can close a loop (Wrasse Lasso Lab, Found 1).")]
        [SerializeField, Range(10f, 120f)] float slingRope = 42f;
        [Tooltip("lab slStiff: how hard a stretched rope pulls back (1/s²). Low: a bungee. High: a chain.")]
        [SerializeField, Range(20f, 800f)] float slingStiffness = 260f;
        [Tooltip("lab slDamp: damping of the rope's stretch, measured against the moving thumb (1/s).")]
        [SerializeField, Range(0f, 30f)] float slingDamping = 5f;
        [Tooltip("lab slDrag: speed lost per second while the thumb holds the rope (1/s). Low: a whirl keeps going on its own.")]
        [SerializeField, Range(0f, 4f)] float slingDrag = 0.35f;
        [Tooltip("lab slFreeDrag: after the thumb lets go, how fast the free kite slows (1/s).")]
        [SerializeField, Range(0f, 8f)] float slingFreeDrag = 0.9f;
        [Tooltip("lab slMaxSpeed: the slung kite's top speed (u/s). 364 = 70% of the lab's 520 (Garrett, 2026-10-20: " +
                 "\"slow down the max speed of the wrasse to 70% of current\").")]
        [SerializeField, Range(100f, 1200f)] float slingMaxSpeed = 364f;
        [Tooltip("Sling on a thumbstick: the stick is the hand's place on the board, as a mouse is - centred is the board's " +
                 "centre, a full lean this many board radii out. Circle the stick and the hand circles; small circles whirl " +
                 "the kite.")]
        [SerializeField, Range(0.1f, 1f)] float slingStickReach = 0.6f;
        [Tooltip("Sling, the AI squad mate: the radius of its whirling hand circle as a share of the rope (the Wrasse " +
                 "Lasso Lab's sling pilot, 0.25). The hand leads the kite a quarter turn round the cluster.")]
        [SerializeField, Range(0.05f, 1f)] float aiSlingLead = 0.25f;

        [Header("Races: the Wrasse collects the rings (Wrasse Run, 2026-10-20)")]
        [Tooltip("While a gate race has lit a ring for this pilot, Time motes come this much more often (added to their " +
                 "weight; base 1 + 1 per petal), among the others: a Time loop saves the boosts that reach the next ring.")]
        [SerializeField, Range(0f, 8f)] float raceTimeMoteBonus = 2f;
        [Tooltip("While a gate race has lit a ring, the AI squad mate values a Time loop this many times a loop of another " +
                 "element the same size. 1 = no preference.")]
        [SerializeField, Range(1f, 8f)] float raceTimeLoopPreference = 3f;

        [Header("Motes (navigator only)")]
        [Tooltip("Where the motes live. Board is the signed-off home.")]
        [SerializeField] KiteMoteHome moteHome = KiteMoteHome.Board;
        [Tooltip("lab moteCount: motes kept near the board. Only the navigator's screen draws them.")]
        [SerializeField, Range(4, 80)] int moteCount = 26;
        [Tooltip("lab moteCluster: average motes per same-element cluster. With growth 2, this and the spread set how big loops get.")]
        [SerializeField, Range(1, 10)] int moteCluster = 4;
        [Tooltip("lab cluster spread (u, gaussian). A balance knob as strong as the growth rate: 14 -> 18 cut the lab's Space blasts 27x (L-STU-110).")]
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
                 "right trigger (right Shift on a keyboard; lifting both thumbs on a touch screen).")]
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
        [Tooltip("An AI-flown Wrasse (an AI crew this machine simulates) is flown by TWO AIs: the hull's AIPilot flies the " +
                 "Grouper and decides when to spend a saved boost, and a navigator AI flies the Wrasse's kite (Garrett, " +
                 "2026-10-20). Off: an AI hull seats no kite and threads rings with the hull.")]
        [SerializeField] bool aiCrewNavigates = true;
        [Tooltip("An AI Grouper spends a saved boost while the next race ring is at least this far (u) and roughly ahead; " +
                 "outside a race it spends them as they come.")]
        [SerializeField, Min(0f)] float aiBoostRingDistance = 180f;
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
        [Tooltip("The kite view's half height in board radii. 1.04 framed the board; 1.352 = 30% further out (Garrett, " +
                 "2026-10-20: \"zoom out 30% more and center the grouper in the wrasses camera\"). The view is centred on the " +
                 "hull.")]
        [SerializeField, Range(0.5f, 3f)] float viewScale = 1.352f;
        [Tooltip("The kite flies the whole kite view - a rectangle the screen's shape, viewScale board radii high - instead of " +
                 "the round board (Garrett, 2026-10-20: \"Let the wrasse travel the whole screen space\").")]
        [SerializeField] bool wholeScreen = true;
        [Tooltip("The rope from the hand (a thumb, the cursor, the stick's point, the AI's hand) to the kite, drawn in the kite " +
                 "view while it is held (Garrett: \"make the wrasse cord visible to the wrasse player\"). Its width, as a share " +
                 "of the tail's.")]
        [SerializeField, Range(0.1f, 2f)] float cordWidth = 0.6f;
        [Tooltip("How visible the cord is (alpha).")]
        [SerializeField, Range(0f, 1f)] float cordAlpha = 0.75f;

        [Header("The hand line (round 8; Garrett, 2026-10-20)")]
        [Tooltip("\"pick a nice horizontal line just above the ability totems and don't let the cursor travel below that " +
                 "line\": the line sits this far above the top of the ability cards, as a fraction of the screen height. " +
                 "The hand (a thumb, the cursor, the stick's point, the AI's hand) never goes below it; the kite still " +
                 "swings across it. Below it, left of the cards, is the pilot's view while navigating.")]
        [SerializeField, Range(0f, 0.1f)] float handLineGap = 0.012f;
        [Tooltip("The line's height (a fraction of the screen) when the HUD has no ability cards to sit above.")]
        [SerializeField, Range(0f, 0.5f)] float handLineFallback = 0.2f;
        [Tooltip("How far inside the screen's sides and top the hand stops (u), significantly less than the cord (Garrett, " +
                 "2026-10-21: \"the mouse should be limited by a small margin significantly less than the cord length\"). " +
                 "Its bottom is the hand line.")]
        [SerializeField, Min(0f)] float handEdgeMargin = 10f;
        [Tooltip("How far past any edge of the screen the kite may fly (u) before a safety wall stops it. The cord is what " +
                 "holds it (\"the wrasse should only be limited by the tether\"): from a hand inside the screen it never " +
                 "reaches this; only a long fling does.")]
        [SerializeField, Min(0f)] float kiteOffscreenReach = 240f;
        [Tooltip("A mote's flower radius in the kite view (u). The petals sit in it in the totem's own proportions (the " +
                 "element flower above each ability card).")]
        [SerializeField, Min(0.5f)] float moteSize = 4.5f;
        [Tooltip("Each element's mote colour, sRGB, in the HUD's order (Charge, Mass, Space, Time): four shades of LIME, the " +
                 "corners of the largest tetrahedron of distinct hue, saturation and value inside lime's boundary against " +
                 "Jade, Ruby, Gold, Blue (neutral) and Fire (danger) (Garrett, 2026-10-21). WRITTEN by " +
                 "Tools/Build/kite_mote_colors.py, which derives them from the live palette; --check fails if they drift.")]
        [SerializeField] Color[] elementColors = new Color[4];

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
        [Tooltip("The pilot's stand-in for the Wrasse: a faint kite this many times the kite's radius, drawn in the pilot's " +
                 "view even when the Wrasse's model draws, so the Grouper keeps a larger representative view of the Wrasse.")]
        [SerializeField, Range(1f, 6f)] float pilotKiteScale = 2.5f;
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
        public KiteControl Control => control;
        public float SlingStickReach => slingStickReach;
        public float AISlingLead => aiSlingLead;
        public bool AICrewNavigates => aiCrewNavigates;
        public float AIBoostRingDistance => aiBoostRingDistance;
        public float RaceTimeMoteBonus => raceTimeMoteBonus;
        public float RaceTimeLoopPreference => raceTimeLoopPreference;
        public float MoteBaseWeight => moteBaseWeight;
        public float MoteWeightPerLevel => moteWeightPerLevel;
        public float BoostCooldownSeconds => boostCooldownSeconds;
        public int OmniMotesPerElement => omniMotesPerElement;
        public float OmniClusterRadius => omniClusterRadius;
        public float PipSize => pipSize;
        public float ViewScale => viewScale;
        public bool WholeScreen => wholeScreen;
        public float CordWidth => cordWidth;
        public float CordAlpha => cordAlpha;
        public float HandLineGap => handLineGap;
        public float HandLineFallback => handLineFallback;
        public float HandEdgeMargin => handEdgeMargin;
        public float KiteOffscreenReach => kiteOffscreenReach;
        public float PilotKiteScale => pilotKiteScale;
        /// <summary>The mote colour of element index <paramref name="e"/> (Element - 1); <paramref name="fallback"/> when
        /// the asset carries none (a config authored before 2026-10-21).</summary>
        public Color ElementColor(int e, Color fallback) =>
            elementColors != null && e >= 0 && e < elementColors.Length && elementColors[e].a > 0f ? elementColors[e] : fallback;
        public float HeadingAlignDistance => headingAlignDistance;
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
            SlingRope = slingRope, SlingStiffness = slingStiffness, SlingDamping = slingDamping,
            SlingDrag = slingDrag, SlingFreeDrag = slingFreeDrag, SlingMaxSpeed = slingMaxSpeed,
        };
    }
}
