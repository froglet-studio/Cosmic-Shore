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
        [Tooltip("Never drawn (a card can be parked while its effect is reworked; lab round 6 parks none).")]
        public bool parked;
    }

    /// <summary>How a trigger upgrade is used: one button, no combos (lab round 5: "not so hard to use like a dolphin combo").</summary>
    public enum HoneyguideTriggerUse
    {
        Press = 0,    // each press is one use, then the cooldown
        Hold = 1,     // it works while the trigger is held
        Charge = 2,   // hold to charge, let go to use it
    }

    /// <summary>One trigger upgrade (the lab's <c>ACTIVES</c>): the card that grants it, the trigger it takes over (0 RT, the
    /// attack; 1 LT, the move), how it is used, its cooldown and its reach at level I.</summary>
    [Serializable]
    public sealed class HoneyguideUpgrade
    {
        public HoneyguideEffect effect;
        [Tooltip("0 RT (always an attack), 1 LT (always a move). Garrett 2026-10-21.")]
        [Range(0, 1)] public int trigger;
        public HoneyguideTriggerUse use;
        [Tooltip("Seconds between uses.")]
        [Min(0f)] public float cooldown;
        [Tooltip("Its reach at level I (u); a repeat grows it by activeLevelGain a level.")]
        [Min(0f)] public float reach;
        [Tooltip("Plays on each use. Ship empty; an unwired slot is a visible TODO.")]
        public EventReference sfx;
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

    /// <summary>One Ratel-AI difficulty in hive mode (the Ratel Raid Lab's SKILL): seconds per cell it drags, whether it plays
    /// the solver's plan (never touching danger), and how often its thumb slides on into a danger cell.</summary>
    [Serializable]
    public sealed class HoneyguideHiveSkill
    {
        [Min(0.02f)] public float cell = 0.22f;
        public bool solver = true;
        [Range(0f, 1f)] public float sloppy;
    }

    public enum HoneyguideAILevel
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
    }

    /// <summary>Which way up the Ratel's big board is drawn. The screen stays locked to landscape; a portrait board is drawn
    /// turned a quarter so it reads upright with the phone held portrait (Garrett 2026-10-22).</summary>
    public enum HoneyguideBoardOrientation
    {
        PortraitOnPhone = 0,
        Portrait = 1,
        Landscape = 2,
    }

    /// <summary>
    /// Every number, table and word of the Honeyguide and Ratel (Honeyguide Lab round 6; <c>Docs/Studios/HONEYGUIDE_LAB.md</c>,
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
        [Tooltip("lab apBank: most AP held; above it a spin's AP are wasted. Garrett 2026-10-29: 6 (was 3), so what a spin does " +
                 "not use is kept.")]
        [SerializeField, Min(1)] int apBank = 6;
        [Tooltip("lab abilityChoices: abilities offered every patternsPerShuffle patterns; the Ratel picks one while the clock is " +
                 "stopped (Garrett 2026-10-29: 3). 1 = the old random draw.")]
        [SerializeField, Range(1, 4)] int abilityChoices = 3;
        [Tooltip("lab pickSeconds: seconds the Ratel has to pick before the first offer is taken; 0 waits for the pick.")]
        [SerializeField, Min(0f)] float pickSeconds = 10f;
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
        [Tooltip("Seconds a made pattern's juice plays on the board: its cells burst, its card pops, the board's edge flashes in " +
                 "the card's family colour (Garrett 2026-10-23: \"the ratel needs juice when it completes a pattern\").")]
        [SerializeField, Min(0f)] float madeJuiceSeconds = 0.7f;
        [Tooltip("Seconds the prism a made pattern takes flies from its cell to its battery slot, morphing from the board's hex " +
                 "prism into the battery's round charge.")]
        [SerializeField, Min(0f)] float chargeFlightSeconds = 0.55f;
        [Tooltip("lab opts.portsCount: an empty port counts as a prism of its colour for completing a pattern.")]
        [SerializeField] bool portsCount = true;
        [Tooltip("lab opts.rotatable: patterns match in any of the six rotations.")]
        [SerializeField] bool rotatable = true;
        [Tooltip("lab RETIRED_ABILITIES: abilities the Ratel never draws. Garrett 2026-10-22: Printer (a free prism at the start " +
                 "of every spin) is out for now, \"too confusing to players on mobile\".")]
        [SerializeField] DynamoAbility[] retiredAbilities = { DynamoAbility.Printer };

        [Header("Hive mode (the Ratel Raid Lab, Garrett 2026-10-31; HONEYGUIDE.md \"Hive mode\")")]
        [Tooltip("An omnicrystal the hull collects becomes a hive, and the Ratel goes into hive mode.")]
        [SerializeField] bool hiveMode = true;
        [Tooltip("lab hiveEndsSpin 'complete': entering hive mode completes the current spin at once (\"the ratel ends their " +
                 "current timer\"), and a fresh spin starts on the way out. Off: the clock only stops, and the old spin resumes.")]
        [SerializeField] bool hiveCompletesSpin = true;
        [Tooltip("lab raidSeconds: hive mode ends when the hive is cleared, after this long, or when the Ratel leaves.")]
        [SerializeField, Min(1f)] float raidSeconds = 15f;
        [Tooltip("lab minChain (Garrett: 3): a chain is a path through touching cells of one other domain, at least this long.")]
        [SerializeField, Min(2)] int minChain = 3;
        [Tooltip("lab snakeMin: the generator lays each other-domain run as a snake at least this long.")]
        [SerializeField, Min(2)] int snakeMin = 3;
        [Tooltip("lab snakeMax: and at most this long.")]
        [SerializeField, Min(2)] int snakeMax = 5;
        [Tooltip("lab hiveOtherShare: share of the 37 cells laid as the other two domains (the rest is ours).")]
        [SerializeField, Range(0.1f, 0.8f)] float hiveOtherShare = 0.45f;
        [Tooltip("lab hiveHoles: empty cells in a hive.")]
        [SerializeField, Min(0)] int hiveHoles = 2;
        [Tooltip("lab hiveCross: chance a later snake crosses a plain cell of its colour, making one more shielded prism.")]
        [SerializeField, Range(0f, 1f)] float hiveCross = 0.35f;
        [Tooltip("lab hiveDangerMin (Garrett: \"always include some danger prisms\"): danger prisms in every hive, off every snake.")]
        [SerializeField, Min(0)] int hiveDangerMin = 2;
        [Tooltip("lab hiveArmour: share of our own body cells that are shielded (at least one).")]
        [SerializeField, Range(0f, 1f)] float hiveArmour = 0.15f;
        [Tooltip("lab stingSeconds: a chain that touches danger breaks with nothing destroyed, and the thumb is stunned this long.")]
        [SerializeField, Min(0f)] float stingSeconds = 1f;
        [Tooltip("lab rewardDynamo (Garrett: 2): prisms of a completed colour that land on the Dynamo.")]
        [SerializeField, Min(0)] int rewardDynamo = 2;
        [Tooltip("lab rewardBattery (Garrett: 1): charges of a completed colour that go to the battery.")]
        [SerializeField, Min(0)] int rewardBattery = 1;
        [Tooltip("lab crystalGlowSeconds: how long the planar crystal lights while the rewards land.")]
        [SerializeField, Min(0f)] float crystalGlowSeconds = 1.6f;
        [Tooltip("The hive in the world: the distance between neighbouring cells' centres is this times the square root of 3 (u).")]
        [SerializeField, Min(0.5f)] float hiveCellSpacing = 2.5f;
        [Tooltip("The hive in the world is laid this far behind where the omnicrystal was taken, square to the hull's flight, " +
                 "so the hull that took it is not inside it (u).")]
        [SerializeField, Min(0f)] float hiveOffset = 40f;
        [Tooltip("Garrett 2026-10-31: the hive the Ratel leaves spawns bees of the player's domain that guard it and grow it. A " +
                 "native bee hive (a Bee Hive Flora config) is planted behind the comb when hive mode ends, in our domain; its bees " +
                 "guard the comb and grow the hive from mass stung off opposing fauna. Empty = no bees.")]
        [SerializeField] FloraConfigurationSO leftBeeHive;
        [Tooltip("How far behind the comb's back face the bee hive is planted (u), so its own comb does not grow into ours.")]
        [SerializeField, Min(0f)] float leftBeeHiveBehind = 12f;
        [Tooltip("The Ratel AI in hive mode, by engineerLevel (Easy, Medium, Hard; the lab's rookie and skilled are Easy and Hard).")]
        [SerializeField] HoneyguideHiveSkill[] hiveSkills =
        {
            new() { cell = 0.5f, solver = false, sloppy = 0.15f },
            new() { cell = 0.35f, solver = false, sloppy = 0.05f },
            new() { cell = 0.22f, solver = true, sloppy = 0f },
        };
        [Tooltip("The strip over the Ratel's board: the farthest omnicrystal it draws at its far end (u). Nearer ones sit along it.")]
        [SerializeField, Min(10f)] float stripRangeU = 1600f;
        [Tooltip("The danger prisms' colour on the Ratel's board.")]
        [SerializeField] Color hiveDangerColour = new(0.85f, 0.12f, 0.08f, 1f);

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
        [Tooltip("A slip is a ride, not a teleport (Garrett 2026-10-21): the hull zips along the trail at this speed (u/s).")]
        [SerializeField, Min(10f)] float zipSpeed = 480f;
        [Tooltip("Seconds the zip takes to reach zipSpeed from the hull's own speed.")]
        [SerializeField, Min(0f)] float zipRamp = 0.15f;
        [Tooltip("How quickly the hull turns onto each leg of the trail while zipping (1/s; higher is snappier).")]
        [SerializeField, Min(0.5f)] float zipTurnRate = 14f;
        [Tooltip("The zip rides this far off the trail's prisms, on the hull's up side (u).")]
        [SerializeField, Min(0f)] float zipLift = 3f;
        [Tooltip("Seconds of boost the hull carries out of a zip.")]
        [SerializeField, Min(0f)] float zipExitBoost = 0.75f;

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

        [Header("The triggers (lab round 6: RT Peck and LT Flit, taken over by the upgrade cards)")]
        [Tooltip("RT's base power, Peck: a weak shot while held, through the hull's own gun (HoneyguidePeckAction.asset, a " +
                 "FullAutoActionSO with the lab's peckRate and peckRange).")]
        [SerializeField] ShipActionSO peckAction;
        [Tooltip("lab peckRange: how far a Peck flies (u). The AI pecks at a hostile prism this close ahead.")]
        [SerializeField, Min(1f)] float peckRange = 45f;
        [Tooltip("lab flitDistance: LT's base power, Flit, throws the hull this far ahead (u).")]
        [SerializeField, Min(1f)] float flitDistance = 18f;
        [Tooltip("lab: the seconds a Flit takes.")]
        [SerializeField, Min(0.02f)] float flitSeconds = 0.15f;
        [Tooltip("lab flitCooldown: seconds between Flits.")]
        [SerializeField, Min(0f)] float flitCooldown = 2f;
        [Tooltip("lab activeSeconds: how long an upgrade holds its trigger before the base power comes back. A repeat refreshes it.")]
        [SerializeField, Min(1f)] float activeSeconds = 20f;
        [Tooltip("lab activeLevelGain: a repeat of the same upgrade grows its reach this much a level (to passiveMaxLevel).")]
        [SerializeField, Min(0f)] float activeLevelGain = 0.25f;
        [Tooltip("The thirteen upgrades (lab ACTIVES), one per upgrade card.")]
        [SerializeField] List<HoneyguideUpgrade> upgrades = new();
        [Tooltip("Hard Light Lasso: the target must be within this many degrees of the nose.")]
        [SerializeField, Range(5f, 90f)] float lassoCone = 30f;
        [Tooltip("Hard Light Lasso: the reel's speed (u/s; the hull's own when faster).")]
        [SerializeField, Min(1f)] float lassoReelSpeed = 220f;
        [Tooltip("Hard Light Lasso: the longest a reel lasts (s).")]
        [SerializeField, Min(0.1f)] float lassoReelSeconds = 1.2f;
        [Tooltip("Hard Light Lasso: on arrival, hostile prisms this close shatter (u, at level I).")]
        [SerializeField, Min(0f)] float lassoBurst = 10f;
        [Tooltip("Genesis Lance: hostile prisms this close to its line shatter (u).")]
        [SerializeField, Min(0.5f)] float lanceWidth = 4f;
        [Tooltip("Genesis Lance: its lane starts this far ahead (u); the lane's prisms are condenserSpacing apart.")]
        [SerializeField, Min(0f)] float lanceLaneStart = 25f;
        [Tooltip("Little Bang Grenade: lobbed this far ahead (u).")]
        [SerializeField, Min(1f)] float grenadeThrow = 55f;
        [Tooltip("Little Bang Grenade: seconds until it bursts.")]
        [SerializeField, Min(0f)] float grenadeFuse = 0.6f;
        [Tooltip("Sepia Drone: seconds it circles.")]
        [SerializeField, Min(0.5f)] float droneSeconds = 8f;
        [Tooltip("Sepia Drone: zaps a second.")]
        [SerializeField, Min(0.1f)] float droneZapsPerSecond = 3f;
        [Tooltip("Particle Harmonizer: the beam's cone (degrees either side of the nose).")]
        [SerializeField, Range(5f, 90f)] float harmonizerCone = 20f;
        [Tooltip("Particle Harmonizer: prisms turned a second while held.")]
        [SerializeField, Min(0.5f)] float harmonizerPerSecond = 8f;
        [Tooltip("Fugacity Discharger: the burst's radius with no charge (u); a full charge reaches the upgrade's reach.")]
        [SerializeField, Min(0f)] float fugacityMinRadius = 12f;
        [Tooltip("Fugacity Discharger: seconds of hold for a full charge.")]
        [SerializeField, Min(0.1f)] float fugacityFullCharge = 1.2f;
        [Tooltip("Fugacity Discharger: boost seconds for each prism it shatters...")]
        [SerializeField, Min(0f)] float fugacityBoostEach = 0.05f;
        [Tooltip("...up to this many.")]
        [SerializeField, Min(0f)] float fugacityBoostCap = 1.5f;
        [Tooltip("Hexapod Eyelet Jet: the seconds the jet takes.")]
        [SerializeField, Min(0.02f)] float eyeletSeconds = 0.25f;
        [Tooltip("Thruster Swarm: the seconds a thrust takes.")]
        [SerializeField, Min(0.02f)] float thrusterSeconds = 0.12f;
        [Tooltip("Rosen Bridge, FTL Chromatool, Monte Cannon: a target must be within this many degrees of the nose.")]
        [SerializeField, Range(5f, 90f)] float jumpCone = 35f;
        [Tooltip("lab: a jump target must be at least this far away (u).")]
        [SerializeField, Min(0f)] float jumpMinDistance = 8f;
        [Tooltip("All Terrain Portals: no prism within this of the landing is empty space, and it goes again (u).")]
        [SerializeField, Min(1f)] float portalsEmptyRadius = 12f;
        [Tooltip("Monte Cannon: hops.")]
        [SerializeField, Min(1)] int monteHops = 3;
        [Tooltip("The AI pilot's look at its triggers, every this many seconds.")]
        [SerializeField, Min(0.05f)] float aiTriggerThink = 0.3f;

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
        [Tooltip("The board's rect while the human plays the Ratel (fractions of the screen: x, y, width, height). Garrett 2026-10-21: it covers the centre of the screen.")]
        [SerializeField] Rect engineerRect = new(0.18f, 0.04f, 0.64f, 0.92f);
        [Tooltip("The board's rect while the human flies (the small view; a double tap on it swaps seats).")]
        [SerializeField] Rect pilotRect = new(0.01f, 0.30f, 0.20f, 0.42f);
        [Tooltip("Which way up the big board is drawn. PortraitOnPhone: upright for a phone held portrait on a touch device, " +
                 "landscape elsewhere. The screen itself stays landscape (Garrett 2026-10-22).")]
        [SerializeField] HoneyguideBoardOrientation boardOrientation = HoneyguideBoardOrientation.PortraitOnPhone;
        [Tooltip("The portrait board's rect (fractions of the portrait safe area: x, y, width, height). The Fly button sits above it.")]
        [SerializeField] Rect portraitRect = new(0.03f, 0.02f, 0.94f, 0.86f);
        [Tooltip("Portrait: the board's text size (the em height, in millimetres on the screen).")]
        [SerializeField, Min(1f)] float portraitTextMillimetres = 2.8f;
        [Tooltip("Portrait: the pattern cards' height, in millimetres on the screen (thumb-sized).")]
        [SerializeField, Min(8f)] float portraitCardMillimetres = 20f;
        [Tooltip("Touch: holding a prism this long (s) without leaving its cell deletes it, for 1 AP (a phone's right-click).")]
        [SerializeField, Min(0.1f)] float longPressSeconds = 0.45f;
        [Tooltip("The Fly button's edge, in millimetres on the screen. It sits just left of the Ratel's board, at its top, and " +
                 "swaps back to flying (a phone has no D-pad and no arrow keys).")]
        [SerializeField, Min(5f)] float flyButtonMillimetres = 10f;
        [Tooltip("The colours of Ruby, Jade and Gold prisms and ports (the game's palette, studio-domains.js).")]
        [SerializeField] Color[] colours = { new(1f, 0f, 0.976f, 1f), new(0.075f, 1f, 0.949f, 1f), new(1f, 0.655f, 0f, 1f) };

        [Header("Words (player-facing; an empty field shows nothing)")]
        [Tooltip("The Ratel's seat label at the top of the board.")]
        [SerializeField] string ratelLabel = "Ratel";
        [Tooltip("The names of colours 1-3 (Ruby, Jade, Gold).")]
        [SerializeField] string[] colourNames = { "Ruby", "Jade", "Gold" };
        [Tooltip("The button beside the Ratel's board that goes back to flying. Empty = the button shows no word (it still works).")]
        [SerializeField] string flyButtonLabel = "Fly";
        [Tooltip("On a lit card. Tokens: none.")]
        [SerializeField] string tapToMake = "TAP";
        [Tooltip("The free-prism line. Tokens: {count}, {colour} (the domain of the prism on the cursor, placed first).")]
        [SerializeField] string freePrismTemplate = "{count} free ({colour} on your cursor): tap an empty cell";
        [Tooltip("The line above the cards naming what the last pattern made does; it stays until the next one. Lime when the " +
                 "effect works on the Dynamo itself (dynamoLineColour). Tokens: {name}, {level} (empty for a one-off), {text} (the " +
                 "card's effect). Empty = no line.")]
        [SerializeField] string madeLineTemplate = "{name}: {text}";
        [Tooltip("The colour of that line (and of the free-prism line) when what it says is about the Dynamo: lime.")]
        [SerializeField] Color dynamoLineColour = new(0.66f, 1f, 0.18f, 1f);
        [Tooltip("The reveal card's heading for a new ability. Tokens: {name}.")]
        [SerializeField] string revealAbilityTemplate = "New ability: {name}";
        [Tooltip("The reveal's second step.")]
        [SerializeField] string revealPortsLine = "New injection ports";
        [Tooltip("The heading over the abilities offered to pick from (round 7). Empty = no heading.")]
        [SerializeField] string pickAbilityTitle = "Pick the Ratel's next ability";
        [Tooltip("Hive mode's heading over the board. Empty = no heading.")]
        [SerializeField] string hiveTitle = "Hive: drag 3 or more of one colour";
        [Tooltip("The Leave button in hive mode (the rewards earned so far are kept).")]
        [SerializeField] string hiveLeaveLabel = "Leave";
        [Tooltip("The toast when an omnicrystal becomes a hive.")]
        [SerializeField] string hiveEnterToast = "The omnicrystal became a hive: hive mode";
        [Tooltip("The toast when a colour is completed. Tokens: {colour}, {prisms}, {charges}.")]
        [SerializeField] string hiveColourDoneTemplate = "{colour} complete: +{prisms} prisms, +{charges} charge";
        [Tooltip("Added when both other colours are done and our own is rewarded too. Tokens: {colour}.")]
        [SerializeField] string hiveBothDoneTemplate = "Both done: {colour} too";
        [Tooltip("The toast when a chain touches a danger prism.")]
        [SerializeField] string hiveStungToast = "Stung: the chain broke";
        [Tooltip("The band above the cards in hive mode: the rewards earned so far. Tokens: {colours}. Empty = no line.")]
        [SerializeField] string hiveEarnedTemplate = "Earned: {colours}";
        [Tooltip("The toast when hive mode ends. Tokens: {rewards} (hiveRewardsTemplate, or hiveNoRewardsLine).")]
        [SerializeField] string hiveEndTemplate = "Hive mode over: {rewards}";
        [Tooltip("{rewards} when a colour was completed. Tokens: {colours}.")]
        [SerializeField] string hiveRewardsTemplate = "{colours} rewards land on the crystal";
        [Tooltip("{rewards} when no colour was completed.")]
        [SerializeField] string hiveNoRewardsLine = "no colour completed";
        [Tooltip("The strip over the board while the Honeyguide closes on an omnicrystal. Tokens: {distance} (u).")]
        [SerializeField] string stripOmniTemplate = "Omnicrystal {distance} u";
        [Tooltip("The strip while no omnicrystal is out. Empty = no line.")]
        [SerializeField] string stripNoTargetLine = "No omnicrystal in sight";
        [Tooltip("The toast when a pattern is made (the game's toast, top right under Settings). Tokens: {name}, {level} (empty for " +
                 "a one-off), {text} (the card's effect).")]
        [SerializeField] string madeToastTemplate = "{name} {level}: {text}";
        [Tooltip("The toast when the Ratel draws a new ability; it tells both seats what it does. Tokens: {name}, {text}.")]
        [SerializeField] string abilityToastTemplate = "The Ratel's new ability, {name}: {text}";
        [Tooltip("A right-clicked card's details on the board. Tokens: {name}, {text}, {uses} (made so far), {max} (made before " +
                 "it is replaced).")]
        [SerializeField] string cardDetailTemplate = "{name}\n{text}\nMade {uses} of {max}\n\nClick anywhere to close";
        [Tooltip("The toast when a card puts an upgrade on a trigger. Tokens: {name}, {level} (empty at I), {trigger}, {text}.")]
        [SerializeField] string upgradeToastTemplate = "{name} {level} on {trigger}: {text}";
        [Tooltip("The triggers' names (RT, LT).")]
        [SerializeField] string[] triggerNames = { "RT", "LT" };
        [Tooltip("The base powers' names (RT, LT).")]
        [SerializeField] string[] basePowerNames = { "Peck", "Flit" };
        [Tooltip("One trigger on the chip beside the small board. Tokens: {trigger}, {power}.")]
        [SerializeField] string triggerChipTemplate = "{trigger} {power}";
        [Tooltip("{power} while an upgrade holds the trigger. Tokens: {name}, {level} (empty at I), {seconds} (left).")]
        [SerializeField] string upgradePowerTemplate = "{name} {level} {seconds}s";
        [Tooltip("Shown when a jump upgrade finds no target.")]
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
        [SerializeField] EventReference flitSfx;
        [SerializeField] EventReference upgradeSfx;
        [SerializeField] EventReference spinSfx;
        [SerializeField] EventReference revealSfx;
        [SerializeField] EventReference hiveEnterSfx;
        [SerializeField] EventReference hiveChainSfx;
        [SerializeField] EventReference hiveStingSfx;
        [SerializeField] EventReference hiveColourSfx;
        [SerializeField] EventReference hiveLeaveSfx;

        [Header("Wiring")]
        [Tooltip("Runtime GameData (the players, for the AI crew rule).")]
        [SerializeField] GameDataSO gameData;

        public float TickSeconds => tickSeconds;
        public float SpinAnim => spinAnim;
        public float MadeJuiceSeconds => madeJuiceSeconds;
        public float ChargeFlightSeconds => chargeFlightSeconds;
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
        public ShipActionSO PeckAction => peckAction;
        public float PeckRange => peckRange;
        public float FlitDistance => flitDistance;
        public float FlitSeconds => flitSeconds;
        public float FlitCooldown => flitCooldown;
        public float ActiveSeconds => activeSeconds;
        public float ActiveLevelGain => activeLevelGain;
        public IReadOnlyList<HoneyguideUpgrade> Upgrades => upgrades;
        public float LassoCone => lassoCone;
        public float LassoReelSpeed => lassoReelSpeed;
        public float LassoReelSeconds => lassoReelSeconds;
        public float LassoBurst => lassoBurst;
        public float LanceWidth => lanceWidth;
        public float LanceLaneStart => lanceLaneStart;
        public float GrenadeThrow => grenadeThrow;
        public float GrenadeFuse => grenadeFuse;
        public float DroneSeconds => droneSeconds;
        public float DroneZapsPerSecond => droneZapsPerSecond;
        public float HarmonizerCone => harmonizerCone;
        public float HarmonizerPerSecond => harmonizerPerSecond;
        public float FugacityMinRadius => fugacityMinRadius;
        public float FugacityFullCharge => fugacityFullCharge;
        public float FugacityBoostEach => fugacityBoostEach;
        public float FugacityBoostCap => fugacityBoostCap;
        public float EyeletSeconds => eyeletSeconds;
        public float ThrusterSeconds => thrusterSeconds;
        public float JumpCone => jumpCone;
        public float JumpMinDistance => jumpMinDistance;
        public float PortalsEmptyRadius => portalsEmptyRadius;
        public int MonteHops => monteHops;
        public float AITriggerThink => aiTriggerThink;
        public HoneyguideEngineerSkill EngineerSkill => engineerSkills is { Length: > 0 }
            ? engineerSkills[Mathf.Clamp((int)engineerLevel, 0, engineerSkills.Length - 1)] : new HoneyguideEngineerSkill();
        public bool AICrewEngineers => aiCrewEngineers;
        public Rect EngineerRect => engineerRect;
        public Rect PilotRect => pilotRect;
        public HoneyguideBoardOrientation BoardOrientation => boardOrientation;
        public Rect PortraitRect => portraitRect;
        public float PortraitTextMillimetres => portraitTextMillimetres;
        public float PortraitCardMillimetres => portraitCardMillimetres;
        public float LongPressSeconds => longPressSeconds;
        public float FlyButtonMillimetres => flyButtonMillimetres;
        public string RatelLabel => ratelLabel;
        public string FlyButtonLabel => flyButtonLabel;
        public string TapToMake => tapToMake;
        public string FreePrismTemplate => freePrismTemplate;
        public string RevealAbilityTemplate => revealAbilityTemplate;
        public string RevealPortsLine => revealPortsLine;
        public string PickAbilityTitle => pickAbilityTitle;
        public string MadeToastTemplate => madeToastTemplate;
        public string MadeLineTemplate => madeLineTemplate;
        public Color DynamoLineColour => dynamoLineColour;
        public string AbilityToastTemplate => abilityToastTemplate;
        public string CardDetailTemplate => cardDetailTemplate;
        public float ZipSpeed => zipSpeed;
        public float ZipRamp => zipRamp;
        public float ZipTurnRate => zipTurnRate;
        public float ZipLift => zipLift;
        public float ZipExitBoost => zipExitBoost;
        public int UsesPerPattern => usesPerPattern;
        public string UpgradeToastTemplate => upgradeToastTemplate;
        public string TriggerChipTemplate => triggerChipTemplate;
        public string UpgradePowerTemplate => upgradePowerTemplate;
        public string NoJumpTarget => noJumpTarget;
        public IReadOnlyList<HoneyguidePattern> Patterns => patterns;
        public IReadOnlyList<HoneyguideEngine> Engines => engines;
        public EventReference PatternMadeSfx => patternMadeSfx;
        public EventReference SlipSfx => slipSfx;
        public EventReference JumpSfx => jumpSfx;
        public EventReference FlitSfx => flitSfx;
        public EventReference UpgradeSfx => upgradeSfx;
        public EventReference SpinSfx => spinSfx;
        public EventReference RevealSfx => revealSfx;
        public EventReference HiveEnterSfx => hiveEnterSfx;
        public EventReference HiveChainSfx => hiveChainSfx;
        public EventReference HiveStingSfx => hiveStingSfx;
        public EventReference HiveColourSfx => hiveColourSfx;
        public EventReference HiveLeaveSfx => hiveLeaveSfx;
        public bool HiveMode => hiveMode;
        public bool HiveCompletesSpin => hiveCompletesSpin;
        public float CrystalGlowSeconds => crystalGlowSeconds;
        public float HiveCellSpacing => hiveCellSpacing;
        public float HiveOffset => hiveOffset;
        public FloraConfigurationSO LeftBeeHive => leftBeeHive;
        public float LeftBeeHiveBehind => leftBeeHiveBehind;
        public float StripRangeU => stripRangeU;
        public Color HiveDangerColour => hiveDangerColour;
        public HoneyguideHiveSkill HiveSkill => hiveSkills is { Length: > 0 }
            ? hiveSkills[Mathf.Clamp((int)engineerLevel, 0, hiveSkills.Length - 1)] : new HoneyguideHiveSkill();
        public string HiveTitle => hiveTitle;
        public string HiveLeaveLabel => hiveLeaveLabel;
        public string HiveEnterToast => hiveEnterToast;
        public string HiveColourDoneTemplate => hiveColourDoneTemplate;
        public string HiveBothDoneTemplate => hiveBothDoneTemplate;
        public string HiveStungToast => hiveStungToast;
        public string HiveEarnedTemplate => hiveEarnedTemplate;
        public string HiveEndTemplate => hiveEndTemplate;
        public string HiveRewardsTemplate => hiveRewardsTemplate;
        public string HiveNoRewardsLine => hiveNoRewardsLine;
        public string StripOmniTemplate => stripOmniTemplate;
        public string StripNoTargetLine => stripNoTargetLine;

        /// <summary>The hive core's settings from this asset (the Ratel Raid Lab's SHIPPED).</summary>
        public HiveSettings ToHiveSettings() => new()
        {
            MinChain = minChain, SnakeMin = snakeMin, SnakeMax = snakeMax, HiveHoles = hiveHoles, HiveDangerMin = hiveDangerMin,
            RewardDynamo = rewardDynamo, RewardBattery = rewardBattery, HiveOtherShare = hiveOtherShare, HiveCross = hiveCross,
            HiveArmour = hiveArmour, StingSeconds = stingSeconds, RaidSeconds = raidSeconds,
        };
        public GameDataSO GameData => gameData;

        /// <summary>Colour 1-3's display colour (white for anything else).</summary>
        public Color ColourOf(int colour) => colours != null && colour >= 1 && colour <= colours.Length ? colours[colour - 1] : Color.white;

        /// <summary>Colour 1-3's name, or empty.</summary>
        public string ColourName(int colour) => colourNames != null && colour >= 1 && colour <= colourNames.Length ? colourNames[colour - 1] : "";

        /// <summary>Trigger k's name (0 RT, 1 LT), or empty.</summary>
        public string TriggerName(int k) => triggerNames != null && k >= 0 && k < triggerNames.Length ? triggerNames[k] : "";

        /// <summary>Trigger k's base power's name (0 Peck, 1 Flit), or empty.</summary>
        public string BasePowerName(int k) => basePowerNames != null && k >= 0 && k < basePowerNames.Length ? basePowerNames[k] : "";

        /// <summary>The upgrade a card grants, or null (the card is not a trigger upgrade).</summary>
        public HoneyguideUpgrade UpgradeFor(HoneyguideEffect effect)
        {
            if (upgrades != null) foreach (var u in upgrades) if (u != null && u.effect == effect) return u;
            return null;
        }

        /// <summary>A Dynamo colour's domain: 1 Ruby, 2 Jade, 3 Gold.</summary>
        public static Domains DomainOf(int colour) => colour switch { 1 => Domains.Ruby, 2 => Domains.Jade, 3 => Domains.Gold, _ => Domains.Blue };

        /// <summary>A domain's Dynamo colour, or 0 (no prism of the Dynamo is that domain).</summary>
        public static int ColourOf(Domains domain) => domain switch { Domains.Ruby => 1, Domains.Jade => 2, Domains.Gold => 3, _ => 0 };

        /// <summary>True when an effect works on the Dynamo itself - its prisms, ports, AP, spin clock or battery - so the line
        /// naming it above the cards is lime (the Ratel-seat cards, and the hull cards that burn, purge or drain the board).</summary>
        public static bool TouchesDynamo(HoneyguideEffect effect) => effect switch
        {
            HoneyguideEffect.Amanita or HoneyguideEffect.BigRed or HoneyguideEffect.Saddle or HoneyguideEffect.ZeroPoint
                or HoneyguideEffect.Triplesynth or HoneyguideEffect.Translator or HoneyguideEffect.Anode
                or HoneyguideEffect.Printer5D or HoneyguideEffect.Hammer or HoneyguideEffect.Cilia or HoneyguideEffect.Chrono
                or HoneyguideEffect.Meta or HoneyguideEffect.Eti or HoneyguideEffect.Vorticity => true,
            _ => false,
        };

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

        /// <summary>The abilities a random draw picks from: all but <c>retiredAbilities</c> (null when none is retired, or all are).</summary>
        int[] AbilityPool()
        {
            if (retiredAbilities is not { Length: > 0 }) return null;
            var pool = new List<int>();
            for (int a = 0; a < DynamoCore.AbilityCount; a++) if (Array.IndexOf(retiredAbilities, (DynamoAbility)a) < 0) pool.Add(a);
            return pool.Count > 0 ? pool.ToArray() : null;
        }

        /// <summary>The core's settings from this asset (the lab's <c>dynamoSettings</c> with the shipped rule options).</summary>
        public DynamoSettings ToSettings()
        {
            var s = new DynamoSettings
            {
                TickSeconds = tickSeconds, ApPerTick = apPerTick, ApBank = apBank, BonusApCap = bonusApCap, BatteryCap = batteryCap,
                RevealAbility = revealAbility, RevealPorts = revealPorts, AllowRotatedMatches = rotatable, PortsCount = portsCount,
                AbilityChoices = abilityChoices, PickSeconds = pickSeconds,
                SlotCount = 3, StartFilled = true, UsesPerPattern = usesPerPattern, PatternsPerShuffle = patternsPerShuffle,
                Abilities = AbilityPool(),
            };
            foreach (var p in patterns) s.Patterns.Add(new DynamoPattern { Cells = ParseShape(p.shape), Parked = p.parked });
            var parsed = new List<int[][]>();
            foreach (var e in engines) if (e != null && e.rows != null) parsed.Add(DynamoCore.ParseEngine(e.rows));
            s.PortSets = DynamoCore.BuildPortSets(parsed);
            return s;
        }
    }
}
