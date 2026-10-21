using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Each pattern's own effect, converted from its tabletop card (Honeyguide Lab round 4, Garrett 2026-10-21: "Keep the
    /// tabletop names and try to make abilities that convert from the old"). Members are the lab's <c>EFFECTS</c> keys in
    /// card order; values are serialized on the config's pattern table, so each member keeps its value.
    /// </summary>
    public enum HoneyguideEffect
    {
        None = 0,
        Plow = 1,
        Dock = 2,
        Amanita = 3,
        Plankter = 4,
        Skipper = 5,
        Fugacity = 6,
        Thruster = 7,
        Lasso = 8,
        BigRed = 9,
        Eigen = 10,
        Saddle = 11,
        Watt = 12,
        Eyelet = 13,
        Detangler = 14,
        Condenser = 15,
        Sepia = 16,
        ZeroPoint = 17,
        Noodley = 18,
        Triplesynth = 19,
        Harmonizer = 20,
        Lance = 21,
        Translator = 22,
        Anode = 23,
        Printer5D = 24,
        Hammer = 25,
        Cilia = 26,
        Chrono = 27,
        Meta = 28,
        Eti = 29,
        Ftl = 30,
        LittleBang = 31,
        Vorticity = 32,
        Gravity = 33,
        Portals = 34,
        Rosen = 35,
        Frame = 36,
        VoidJumper = 37,
        Blink = 38,
        Monte = 39,
    }

    /// <summary>One tabletop card: its name, family (1 Ruby, 2 Jade, 3 Gold: the printed M / C / S), its shape as printed,
    /// its effect and the words the player reads. Parked cards are never drawn.</summary>
    [Serializable]
    public sealed class HoneyguidePattern
    {
        [Tooltip("The tabletop card's name (player-facing).")]
        public string name;
        [Tooltip("1 Ruby, 2 Jade, 3 Gold (the card's printed family M / C / S).")]
        [Range(1, 3)] public int family = 1;
        [Tooltip("The shape as printed: tokens 'Xq,r' with X = M / C / S (Ruby / Jade / Gold), r running down.")]
        public string shape;
        public HoneyguideEffect effect;
        [Tooltip("What the effect does, as the player reads it on the card.")]
        [TextArea] public string effectText;
        [Tooltip("Never drawn. The trigger effects are parked until the Honeyguide's triggers take upgrades in the game " +
                 "(HONEYGUIDE.md).")]
        public bool parked;
    }

    /// <summary>A designed engine (Appendix B): its printed rows, top first, M / C / S a port of that colour.</summary>
    [Serializable]
    public sealed class HoneyguideEngine
    {
        public string label;
        public string[] rows = new string[7];
    }

    /// <summary>The words for one of the Ratel's abilities.</summary>
    [Serializable]
    public sealed class HoneyguideAbilityText
    {
        public DynamoAbility ability;
        public string name;
        [TextArea] public string text;
    }

    /// <summary>One Engineer-AI difficulty: how often it thinks, how many placements it weighs and how often it blunders.</summary>
    [Serializable]
    public sealed class HoneyguideEngineerSkill
    {
        [Min(0.05f)] public float think = 0.45f;
        [Min(1)] public int top = 1;
        [Range(0f, 1f)] public float blunder;
    }

    public enum HoneyguideAILevel
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
    }

    /// <summary>
    /// Every number, table and word of the Honeyguide and Ratel (Honeyguide Lab round 4; <c>Docs/Studios/HONEYGUIDE_LAB.md</c>,
    /// <c>HONEYGUIDE.md</c> beside this file). The lab is the record: each field names the lab key it came from, and
    /// <c>Tools/Build/author_honeyguide_assets.py --check</c> holds the shipped asset to the page. Read by
    /// <see cref="HoneyguideCrew"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "HoneyguideConfig", menuName = "ScriptableObjects/Vessel Actions/Honeyguide Config")]
    public sealed class HoneyguideConfigSO : ScriptableObject
    {
        [Header("The Dynamo (the Ratel's hex puzzle)")]
        [Tooltip("lab tickSeconds: seconds per spin (the port pattern turns 60 degrees; the AP refill).")]
        [SerializeField, Min(1f)] float tickSeconds = 15f;
        [Tooltip("lab apPerTick: action points each spin (move, inject and make cost 1).")]
        [SerializeField, Min(1)] int apPerTick = 3;
        [Tooltip("lab apBank: most AP held; above it a spin's AP are wasted.")]
        [SerializeField, Min(1)] int apBank = 3;
        [Tooltip("lab bonusApCap: banked extra AP (the Refund ability).")]
        [SerializeField, Min(0)] int bonusApCap = 2;
        [Tooltip("lab batteryCap: slip charges held; a new one replaces the oldest when full.")]
        [SerializeField, Min(1)] int batteryCap = 3;
        [Tooltip("lab revealAbility: seconds the spin clock stops to show the Ratel's new ability.")]
        [SerializeField, Min(0f)] float revealAbility = 3.5f;
        [Tooltip("lab revealPorts: then seconds the new port pattern is shown before the clock restarts.")]
        [SerializeField, Min(0f)] float revealPorts = 2f;
        [Tooltip("lab usesPerPattern: a pattern made this many times is replaced (0 = never).")]
        [SerializeField, Min(0)] int usesPerPattern = 3;
        [Tooltip("lab patternsPerShuffle: every this many patterns, a new ability and a new port pattern (0 = never).")]
        [SerializeField, Min(0)] int patternsPerShuffle = 4;
        [Tooltip("lab spinAnim: seconds the ports take to turn on screen.")]
        [SerializeField, Min(0f)] float spinAnim = 0.6f;
        [Tooltip("lab opts.portsCount: an empty port counts as a prism of its colour for completing a pattern.")]
        [SerializeField] bool portsCount = true;
        [Tooltip("lab opts.rotatable: patterns match in any of the six rotations.")]
        [SerializeField] bool rotatable = true;

        [Header("The Slip (the Honeyguide's Time ability)")]
        [Tooltip("lab minSlipTrailLength: shortest trail (prisms) a slip rides.")]
        [SerializeField, Min(2)] int minSlipTrailLength = 6;
        [Tooltip("lab slipReach: farthest a slip lands from where you touched (u).")]
        [SerializeField, Min(10f)] float slipReach = 400f;
        [Tooltip("lab exitOffset: land this far past the trail's end, outward (u).")]
        [SerializeField, Min(0f)] float exitOffset = 8f;
        [Tooltip("lab slipGrace: seconds the trail just ridden is ignored (no re-slip on landing).")]
        [SerializeField, Min(0f)] float slipGrace = 1f;
        [Tooltip("lab aimCone: the slip arms when the nose is within this many degrees of the touched prism's long axis, either way.")]
        [SerializeField, Range(5f, 90f)] float aimCone = 45f;
        [Tooltip("lab hullRadius + prismL / 2: a prism this close to the hull's centre is touched (u).")]
        [SerializeField, Min(0.5f)] float contactRadius = 5.5f;
        [Tooltip("lab selfGrace (SelfTrailContactConfig.hullGraceSeconds): a prism younger than this is never touched.")]
        [SerializeField, Min(0f)] float selfGrace = 1f;
        [Tooltip("lab 'exit: align': the hull lands facing along the trail's outward heading (off: it keeps its facing).")]
        [SerializeField] bool exitAligned = true;

        [Header("Pattern effects (lab applyEffect)")]
        [Tooltip("lab passiveSeconds: how long a timed effect lasts; a repeat refreshes it and raises its level.")]
        [SerializeField, Min(1f)] float passiveSeconds = 20f;
        [Tooltip("lab passiveMaxLevel: highest level a timed effect stacks to.")]
        [SerializeField, Range(1, 5)] int passiveMaxLevel = 3;
        [Tooltip("lab: most boost seconds held at once.")]
        [SerializeField, Min(0.5f)] float boostCap = 6f;
        [Tooltip("Juggernaut Plow: boost seconds when made.")]
        [SerializeField, Min(0f)] float plowBoost = 1f;
        [Tooltip("Amanita Furnace: boost seconds (it burns one Dynamo prism).")]
        [SerializeField, Min(0f)] float amanitaBoost = 3f;
        [Tooltip("Big Red Button: boost seconds for each Dynamo prism destroyed.")]
        [SerializeField, Min(0f)] float bigRedBoostEach = 0.5f;
        [Tooltip("Dragon Saddle: boost seconds for each battery charge drained.")]
        [SerializeField, Min(0f)] float saddleBoostEach = 1.5f;
        [Tooltip("Zero Point Amplifier: boost seconds for the charge it drains.")]
        [SerializeField, Min(0f)] float zeroPointBoost = 1.5f;
        [Tooltip("Plankter Sail: boost seconds after the free slip.")]
        [SerializeField, Min(0f)] float sailBoost = 1f;
        [Tooltip("Plankter Sail: most free slips held.")]
        [SerializeField, Min(1)] int maxSails = 3;
        [Tooltip("Singularity Dock (2 a level) and Gravity Engine (1 a level): slips catch trails this much farther a unit (u).")]
        [SerializeField, Min(0f)] float magnetPerLevel = 3f;
        [Tooltip("Gravity Engine: boost seconds after each slip, a level.")]
        [SerializeField, Min(0f)] float gravityBoostPerLevel = 1f;
        [Tooltip("Anomaly Skipper: boost seconds a level for touching a prism of a different domain from the last.")]
        [SerializeField, Min(0f)] float skipperBoostPerLevel = 0.4f;
        [Tooltip("Eigen Booster: turn rate added a unit (2 units a level).")]
        [SerializeField, Min(0f)] float eigenTurnPerUnit = 0.3f;
        [Tooltip("Wattslider: speed added a level while alongside a trail.")]
        [SerializeField, Min(0f)] float wattSpeedPerLevel = 0.3f;
        [Tooltip("Wattslider: alongside means a trail prism within this distance (u)...")]
        [SerializeField, Min(1f)] float wattReach = 14f;
        [Tooltip("...with the heading within this many degrees of its axis.")]
        [SerializeField, Range(5f, 90f)] float wattCone = 30f;
        [Tooltip("Thruster Swarm: dashes stored.")]
        [SerializeField, Min(1)] int thrusterDashes = 4;
        [Tooltip("Thruster Swarm: each dash throws the hull this far ahead (u).")]
        [SerializeField, Min(1f)] float dashDistance = 20f;
        [Tooltip("Reality Detangler: prisms touched that turn the newest charge's domain.")]
        [SerializeField, Min(1)] int detangleCount = 3;
        [Tooltip("Noodley Appendages: hostile prisms pushed aside.")]
        [SerializeField, Min(1)] int noodleCount = 5;
        [Tooltip("Noodley Appendages: how far each is pushed (u).")]
        [SerializeField, Min(1f)] float noodlePush = 12f;
        [Tooltip("Noodley Appendages: a prism this much farther than a touch is reached (u).")]
        [SerializeField, Min(0f)] float noodleReach = 6f;
        [Tooltip("Epitaxial Condenser: prisms in the lane it lays.")]
        [SerializeField, Min(2)] int condenserCount = 6;
        [Tooltip("Epitaxial Condenser: the lane starts this far ahead (u); its prisms are a prism spacing apart.")]
        [SerializeField, Min(0f)] float condenserAhead = 30f;
        [Tooltip("Epitaxial Condenser: the spacing of its prisms (u; the Sparrow's wavelength).")]
        [SerializeField, Min(1f)] float condenserSpacing = 7f;
        [Tooltip("Mystic Eti: domains with a trail within this distance each give a charge (u).")]
        [SerializeField, Min(1f)] float etiReach = 40f;
        [Tooltip("Square-Peg Hammer: Dynamo prisms dropped.")]
        [SerializeField, Min(1)] int hammerCount = 3;
        [Tooltip("Reactor Cilia: extra AP banked.")]
        [SerializeField, Min(1)] int ciliaBonus = 3;
        [Tooltip("Chrono-Compressor: seconds the spin bar winds back.")]
        [SerializeField, Min(0f)] float chronoRewind = 5f;

        [Header("Stored jumps (the Slip card reads Jump)")]
        [Tooltip("lab: most jumps stored.")]
        [SerializeField, Min(1)] int maxStoredJumps = 3;
        [Tooltip("Rosen Bridge, FTL Chromatool, Monte Cannon: a target must be within this many degrees of the nose.")]
        [SerializeField, Range(5f, 90f)] float jumpCone = 35f;
        [Tooltip("lab: a jump target must be at least this far away (u).")]
        [SerializeField, Min(0f)] float jumpMinDistance = 8f;
        [Tooltip("Blink Drive: jump distance (u).")]
        [SerializeField, Min(1f)] float blinkDistance = 60f;
        [Tooltip("All Terrain Portals: jump distance (u); landing in empty space jumps it again.")]
        [SerializeField, Min(1f)] float portalsHop = 30f;
        [Tooltip("All Terrain Portals: no prism within this of the landing is empty space (u).")]
        [SerializeField, Min(1f)] float portalsEmptyRadius = 12f;
        [Tooltip("Rosen Bridge: the farthest prism of the newest charge's domain within this distance (u).")]
        [SerializeField, Min(10f)] float rosenReach = 300f;
        [Tooltip("FTL Chromatool: the nearest prism ahead within this distance (u).")]
        [SerializeField, Min(1f)] float ftlReach = 60f;
        [Tooltip("Monte Cannon: hops.")]
        [SerializeField, Min(1)] int monteHops = 3;
        [Tooltip("Monte Cannon: each hop's farthest prism within this distance (u).")]
        [SerializeField, Min(1f)] float monteReach = 40f;

        [Header("Crew AI")]
        [Tooltip("The Ratel AI's level when the human flies.")]
        [SerializeField] HoneyguideAILevel engineerLevel = HoneyguideAILevel.Hard;
        [Tooltip("lab ESKILL easy / medium / hard.")]
        [SerializeField] HoneyguideEngineerSkill[] engineerSkills =
        {
            new() { think = 1.6f, top = 4, blunder = 0.25f },
            new() { think = 0.9f, top = 2, blunder = 0.08f },
            new() { think = 0.45f, top = 1, blunder = 0f },
        };
        [Tooltip("A hull flown by its AIPilot on this machine (an AI rival or squad mate) gets an AI Ratel too.")]
        [SerializeField] bool aiCrewEngineers = true;

        [Header("The Ratel's view")]
        [Tooltip("The board's rect while the human plays the Ratel (fractions of the screen: x, y, width, height).")]
        [SerializeField] Rect engineerRect = new(0.5f, 0.04f, 0.48f, 0.92f);
        [Tooltip("The board's rect while the human flies (the small view; a double tap on it swaps seats).")]
        [SerializeField] Rect pilotRect = new(0.01f, 0.30f, 0.20f, 0.42f);
        [Tooltip("The colours of Ruby, Jade and Gold prisms and ports (the game's palette, studio-domains.js).")]
        [SerializeField] Color[] colours = { new(1f, 0f, 0.976f, 1f), new(0.075f, 1f, 0.949f, 1f), new(1f, 0.655f, 0f, 1f) };

        [Header("Words (player-facing; an empty field shows nothing)")]
        [Tooltip("The Ratel's seat label at the top of the board.")]
        [SerializeField] string ratelLabel = "Ratel";
        [Tooltip("The names of colours 1-3 (Ruby, Jade, Gold).")]
        [SerializeField] string[] colourNames = { "Ruby", "Jade", "Gold" };
        [Tooltip("On a lit card. Tokens: none.")]
        [SerializeField] string tapToMake = "TAP";
        [Tooltip("The free-prism line. Tokens: {count}, {colour}.")]
        [SerializeField] string freePrismTemplate = "{count} free: tap an empty cell ({colour}; tap the AP row to change)";
        [Tooltip("The reveal card's heading for a new ability. Tokens: {name}.")]
        [SerializeField] string revealAbilityTemplate = "New ability: {name}";
        [Tooltip("The reveal's second step.")]
        [SerializeField] string revealPortsLine = "New injection ports";
        [Tooltip("The banner when a pattern is made. Tokens: {name}, {level} (empty for a one-off).")]
        [SerializeField] string madeBannerTemplate = "{name} {level}";
        [Tooltip("The Slip card while jumps are stored. Tokens: {count}.")]
        [SerializeField] string jumpCardTemplate = "Jump x{count}";
        [Tooltip("The Slip card while dashes are stored. Tokens: {count}.")]
        [SerializeField] string dashCardTemplate = "Dash x{count}";
        [Tooltip("Shown when a stored jump finds no target.")]
        [SerializeField] string noJumpTarget = "No jump target";
        [Tooltip("The patterns' tabletop cards, in card order (39). Parked cards are never drawn.")]
        [SerializeField] List<HoneyguidePattern> patterns = new();
        [Tooltip("The designed engines; every three-fold colour-symmetric turn and recolouring of them is a port pattern.")]
        [SerializeField] List<HoneyguideEngine> engines = new();
        [Tooltip("The Ratel's abilities' names and words.")]
        [SerializeField] List<HoneyguideAbilityText> abilities = new();

        [Header("Audio (ship empty; an unwired slot is a visible TODO)")]
        [SerializeField] EventReference patternMadeSfx;
        [SerializeField] EventReference slipSfx;
        [SerializeField] EventReference jumpSfx;
        [SerializeField] EventReference spinSfx;
        [SerializeField] EventReference revealSfx;

        [Header("Wiring")]
        [Tooltip("Runtime GameData (the players, for the AI crew rule).")]
        [SerializeField] GameDataSO gameData;

        public float TickSeconds => tickSeconds;
        public float SpinAnim => spinAnim;
        public int MinSlipTrailLength => minSlipTrailLength;
        public float SlipReach => slipReach;
        public float ExitOffset => exitOffset;
        public float SlipGrace => slipGrace;
        public float AimCone => aimCone;
        public float ContactRadius => contactRadius;
        public float SelfGrace => selfGrace;
        public bool ExitAligned => exitAligned;
        public float PassiveSeconds => passiveSeconds;
        public int PassiveMaxLevel => passiveMaxLevel;
        public float BoostCap => boostCap;
        public float PlowBoost => plowBoost;
        public float AmanitaBoost => amanitaBoost;
        public float BigRedBoostEach => bigRedBoostEach;
        public float SaddleBoostEach => saddleBoostEach;
        public float ZeroPointBoost => zeroPointBoost;
        public float SailBoost => sailBoost;
        public int MaxSails => maxSails;
        public float MagnetPerLevel => magnetPerLevel;
        public float GravityBoostPerLevel => gravityBoostPerLevel;
        public float SkipperBoostPerLevel => skipperBoostPerLevel;
        public float EigenTurnPerUnit => eigenTurnPerUnit;
        public float WattSpeedPerLevel => wattSpeedPerLevel;
        public float WattReach => wattReach;
        public float WattCone => wattCone;
        public int ThrusterDashes => thrusterDashes;
        public float DashDistance => dashDistance;
        public int DetangleCount => detangleCount;
        public int NoodleCount => noodleCount;
        public float NoodlePush => noodlePush;
        public float NoodleReach => noodleReach;
        public int CondenserCount => condenserCount;
        public float CondenserAhead => condenserAhead;
        public float CondenserSpacing => condenserSpacing;
        public float EtiReach => etiReach;
        public int HammerCount => hammerCount;
        public int CiliaBonus => ciliaBonus;
        public float ChronoRewind => chronoRewind;
        public int MaxStoredJumps => maxStoredJumps;
        public float JumpCone => jumpCone;
        public float JumpMinDistance => jumpMinDistance;
        public float BlinkDistance => blinkDistance;
        public float PortalsHop => portalsHop;
        public float PortalsEmptyRadius => portalsEmptyRadius;
        public float RosenReach => rosenReach;
        public float FtlReach => ftlReach;
        public int MonteHops => monteHops;
        public float MonteReach => monteReach;
        public HoneyguideEngineerSkill EngineerSkill => engineerSkills is { Length: > 0 }
            ? engineerSkills[Mathf.Clamp((int)engineerLevel, 0, engineerSkills.Length - 1)] : new HoneyguideEngineerSkill();
        public bool AICrewEngineers => aiCrewEngineers;
        public Rect EngineerRect => engineerRect;
        public Rect PilotRect => pilotRect;
        public string RatelLabel => ratelLabel;
        public string TapToMake => tapToMake;
        public string FreePrismTemplate => freePrismTemplate;
        public string RevealAbilityTemplate => revealAbilityTemplate;
        public string RevealPortsLine => revealPortsLine;
        public string MadeBannerTemplate => madeBannerTemplate;
        public string JumpCardTemplate => jumpCardTemplate;
        public string DashCardTemplate => dashCardTemplate;
        public string NoJumpTarget => noJumpTarget;
        public IReadOnlyList<HoneyguidePattern> Patterns => patterns;
        public IReadOnlyList<HoneyguideEngine> Engines => engines;
        public EventReference PatternMadeSfx => patternMadeSfx;
        public EventReference SlipSfx => slipSfx;
        public EventReference JumpSfx => jumpSfx;
        public EventReference SpinSfx => spinSfx;
        public EventReference RevealSfx => revealSfx;
        public GameDataSO GameData => gameData;

        /// <summary>Colour 1-3's display colour (white for anything else).</summary>
        public Color ColourOf(int colour) => colours != null && colour >= 1 && colour <= colours.Length ? colours[colour - 1] : Color.white;

        /// <summary>Colour 1-3's name, or empty.</summary>
        public string ColourName(int colour) => colourNames != null && colour >= 1 && colour <= colourNames.Length ? colourNames[colour - 1] : "";

        /// <summary>A Dynamo colour's domain: 1 Ruby, 2 Jade, 3 Gold.</summary>
        public static Domains DomainOf(int colour) => colour switch { 1 => Domains.Ruby, 2 => Domains.Jade, 3 => Domains.Gold, _ => Domains.Blue };

        /// <summary>A domain's Dynamo colour, or 0 (no prism of the Dynamo is that domain).</summary>
        public static int ColourOf(Domains domain) => domain switch { Domains.Ruby => 1, Domains.Jade => 2, Domains.Gold => 3, _ => 0 };

        public HoneyguideAbilityText AbilityText(DynamoAbility ability)
        {
            if (abilities != null) foreach (var a in abilities) if (a != null && a.ability == ability) return a;
            return null;
        }

        /// <summary>A card's shape as (q, r, colour) cells: the lab's <c>PATTERNS</c> parse.</summary>
        public static int[][] ParseShape(string shape)
        {
            var cells = new List<int[]>();
            if (string.IsNullOrWhiteSpace(shape)) return cells.ToArray();
            foreach (var tok in shape.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                int col = tok[0] == 'M' ? 1 : tok[0] == 'C' ? 2 : tok[0] == 'S' ? 3 : 0;
                var qr = tok.Substring(1).Split(',');
                if (col == 0 || qr.Length != 2 || !int.TryParse(qr[0], out int q) || !int.TryParse(qr[1], out int r)) continue;
                cells.Add(new[] { q, r, col });
            }
            return cells.ToArray();
        }

        /// <summary>The core's settings from this asset (the lab's <c>dynamoSettings</c> with the shipped rule options).</summary>
        public DynamoSettings ToSettings()
        {
            var s = new DynamoSettings
            {
                TickSeconds = tickSeconds, ApPerTick = apPerTick, ApBank = apBank, BonusApCap = bonusApCap, BatteryCap = batteryCap,
                RevealAbility = revealAbility, RevealPorts = revealPorts, AllowRotatedMatches = rotatable, PortsCount = portsCount,
                SlotCount = 3, StartFilled = true, UsesPerPattern = usesPerPattern, PatternsPerShuffle = patternsPerShuffle,
            };
            foreach (var p in patterns) s.Patterns.Add(new DynamoPattern { Cells = ParseShape(p.shape), Parked = p.parked });
            var parsed = new List<int[][]>();
            foreach (var e in engines) if (e != null && e.rows != null) parsed.Add(DynamoCore.ParseEngine(e.rows));
            s.PortSets = DynamoCore.BuildPortSets(parsed);
            return s;
        }
    }
}
