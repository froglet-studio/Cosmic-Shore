using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>voyage settings</b> (Docs/Voyages/SLICE.md): what a voyage sails and how the Voyage window draws it. Not a
    /// toy (the name is history): voyages have their own scene, <c>Voyage.unity</c>, and nothing of them stands in
    /// freestyle (Garrett, 2026-11-01: "we do not need to connect the freestyle and voyage modes"). The app shell's Voyage
    /// window draws one card per job on offer and the Ark escort's card, and Set sail loads the voyage scene with the
    /// choice (<see cref="VoyageLaunch"/>), where <see cref="VoyageHarbour"/> deploys the squad and the Ark sails.
    ///
    /// <para>The Ark, the corridor, the leash and the payloads are the Arkway's own (<see cref="ArkwayRun"/>), sailed from
    /// <see cref="withArk"/>'s settings. The map, crew, jobs and every line are <see cref="VoyageContentSO"/>. The shipped
    /// asset is <c>Resources/Voyages/Voyage</c> (<see cref="Load"/>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Voyage", menuName = "ScriptableObjects/Voyages/Voyage Settings")]
    public class VoyageToyDefinitionSO : ScriptableObject
    {
        /// <summary>Where the shipped settings live, under Resources.</summary>
        public const string ResourcePath = "Voyages/Voyage";

        static VoyageToyDefinitionSO s_loaded;

        /// <summary>The shipped voyage settings (<c>Resources/Voyages/Voyage</c>), or null when the asset is missing.</summary>
        public static VoyageToyDefinitionSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageToyDefinitionSO>(ResourcePath);
            return s_loaded;
        }

        [Header("Identity")]
        [SerializeField, Tooltip("The voyage's name where no content names one.")]
        string displayName = "Voyage";

        [SerializeField, TextArea, Tooltip("One line on what a voyage is.")]
        string description;

        [SerializeField, Tooltip("The voyage's accent colour.")]
        Color accentColor = new(0.95f, 0.75f, 0.25f, 1f);

        [Header("Voyage - what it sails")]
        [SerializeField, Tooltip("The Ark and corridor settings (the Arkway's own asset, Wander_WithArk). Empty = code " +
                                 "defaults with no prism prefab, so no Ark can be laid and the voyage refuses to sail.")]
        ArkwaySettingsSO withArk;

        [SerializeField, Tooltip("The map, crew, jobs and lines. Empty = the shipped Resources/Voyages/VoyageContent.")]
        VoyageContentSO content;

        [SerializeField, Tooltip("The Ark escort: its worlds, its 1-9 difficulty tiers and its lines. Empty = no escort card.")]
        ArkEscortSO arkEscort;

        [Header("Voyage - the squad")]
        [SerializeField, Range(0f, 1f), Tooltip("Element level a seat starts in its slot's element (normalized: 0.1 is one " +
                                                "petal, 0.5 the level-5 twist). Raised to, never lowered.")]
        float seedElementLevel = 0.5f;

        [SerializeField, Min(200f), Tooltip("An AI wingmate further than this from the Ark is brought back to its flank.")]
        float wingmateLeash = 1600f;

        [Header("Voyage - the starter squad")]
        [SerializeField, Tooltip("The name of the squad a player with no squad is given (Shombith, 2026-11-16: the board's " +
                                 "jobs come from the active squad's three pilots only, so every player needs one). Empty " +
                                 "= the squad book's default name.")]
        string starterSquadName = "Starter Squad";

        [SerializeField, Tooltip("The starter squad's three seats, leader first: vessel, element and pilot id (one pilot " +
                                 "per seat, each of a different callsign, so the board offers three different jobs). The " +
                                 "defaults are Garrett's three archetypes (Docs/Voyages/HANDOFF.md section 8.4): a Guardian " +
                                 "in Charge with TALON, a Cultivator Squirrel in Mass with ANVIL, a Destroyer Dolphin in " +
                                 "Space with CITADEL.")]
        SquadSlot[] starterSeats =
        {
            new(VesselClassType.Sparrow, Element.Charge, "pilot.callsign.talon"),
            new(VesselClassType.Squirrel, Element.Mass, "pilot.callsign.anvil"),
            new(VesselClassType.Dolphin, Element.Space, "pilot.callsign.citadel"),
        };

        [Header("Voyage - which voyages can sail (Milestone 1)")]
        [SerializeField, Tooltip("Lock the crew's job voyages, except the ones listed below (Shombith, 2026-11-16: make one " +
                                 "voyage good before the others open). The Ark escort is never locked. Off = every job sails.")]
        bool lockJobs = true;

        [SerializeField, Tooltip("The job templates that still sail while jobs are locked (VoyageJob.TemplateId). Milestone 1 " +
                                 "keeps Sail home (job.return) and the crew's Crystal Run (job.raid.crystals, authored in " +
                                 "voyage_slice.json). Add a template id here to open that job.")]
        string[] openJobs = { VoyageRules.ReturnJobId, CrystalRunJobId };

        /// <summary>The Milestone 1 crew voyage, Crystal Run (Shombith, 2026-11-16: steal elemental crystals on the way).</summary>
        public const string CrystalRunJobId = "job.raid.crystals";

        [Header("Voyage - arrivals")]
        [SerializeField, Min(80f), Tooltip("How far from the Ark a vault shell or a wreck stands.")]
        float findDistance = 520f;

        [SerializeField, Min(4f), Tooltip("A vault shell's radius.")]
        float vaultRadius = 26f;

        [SerializeField, Range(8, 200), Tooltip("Super-shielded prisms in a vault shell.")]
        int vaultPrisms = 40;

        [SerializeField, Tooltip("Each vault prism's scale.")]
        Vector3 vaultPrismScale = new(5f, 5f, 8f);

        [SerializeField, Range(0.05f, 1f), Tooltip("Share of a shell's super-shields that must be cracked before it opens.")]
        float vaultOpenShare = 0.5f;

        [SerializeField, Min(50f), Tooltip("How close to the Ark the pilot must stay for a core's hold to run.")]
        float holdRadius = 700f;

        [SerializeField, Min(100f), Tooltip("A creature felled within this of the Ark counts for a cull (Docs/Voyages/DEPTH.md section 4).")]
        float cullRadius = 900f;

        [SerializeField, Min(20f), Tooltip("A living creature within this of the pilot counts as observed (a researcher's study).")]
        float observeRadius = 260f;

        [SerializeField, Min(100f), Tooltip("How far abeam of the Ark a rival crew's hulls stand at a stop.")]
        float rivalAbeam = 420f;

        [Header("Voyage - the escort's pay (Docs/Voyages/CRYSTAL_RUN.md section 5)")]
        [SerializeField, Min(0), Tooltip("Crystals an Ark escort that came through pays per difficulty step: owed at the last " +
                                         "world, banked when the Ark docks home.")]
        int escortCrystalsPerDifficulty = 30;

        [SerializeField, Tooltip("The element the escort's pay is in.")]
        Element escortRewardElement = Element.Omni;

        [Header("Voyage - the scoreboard's score (a seat's score is the sum of these)")]
        [SerializeField, Min(0), Tooltip("Score per crystal a seat stole.")]
        int scorePerCrystal = 10;

        [SerializeField, Min(0), Tooltip("Score per creature a seat felled.")]
        int scorePerKill = 5;

        [SerializeField, Min(0), Tooltip("Extra score per creature felled near the Ark (hull the seat saved).")]
        int scorePerGuard = 15;

        [Header("Voyage - HUD sounds (shipped empty: an unwired slot is a visible TODO)")]
        [SerializeField, Tooltip("A toast that marks an event (setting off, a calm cell, the final cell).")]
        FMODUnity.EventReference toastEventSound;

        [SerializeField, Tooltip("A toast that marks a threat (a hostile cell, an assault, the breach line close).")]
        FMODUnity.EventReference toastThreatSound;

        [SerializeField, Tooltip("A toast that marks something good (the goal reached, an assault repelled).")]
        FMODUnity.EventReference toastGoodSound;

        [SerializeField, Tooltip("The end-of-voyage scoreboard coming up.")]
        FMODUnity.EventReference scoreboardSound;

        [SerializeField, Tooltip("The Ark's horn: it weighs anchor soon (the raid window is closing).")]
        FMODUnity.EventReference arkHornSound;

        [Header("Voyage - Crystal Run raid windows (P1, Shombith 2026-11-16)")]
        [SerializeField, Min(0f), Tooltip("Seconds the Ark anchors in a HOSTILE cell of a Crystal Run before it sails on. " +
                                          "0 = it does not stop.")]
        float raidAnchorHostileSeconds = 30f;

        [SerializeField, Min(0f), Tooltip("Seconds the Ark anchors in a CALM cell of a Crystal Run before it sails on. " +
                                          "0 = it does not stop.")]
        float raidAnchorCalmSeconds = 30f;

        [SerializeField, Min(0f), Tooltip("Seconds before the Ark sails that its horn sounds and the window-closing toast shows.")]
        float raidAnchorWarnSeconds = 10f;

        [Header("Voyage - Crystal Run carried crystals (P6, Shombith 2026-11-16)")]
        [SerializeField, Tooltip("On: a crystal a seat takes is CARRIED until that seat flies within the bank radius of the " +
                                 "Ark, and a hit from a hostile danger prism spills everything it carries back into the " +
                                 "sea. Off: a crystal counts the moment it is taken.")]
        bool raidCrystalsAtRisk = true;

        [SerializeField, Min(10f), Tooltip("How close to the Ark's centre, u, a seat banks what it carries.")]
        float raidBankRadius = 180f;

        [SerializeField, Min(0f), Tooltip("After a spill, seconds before the same seat can spill again.")]
        float raidSpillCooldownSeconds = 1.5f;

        [Header("Voyage - Crystal Run hull raids and the way home (Shombith 2026-11-16, round 3)")]
        [SerializeField, Range(0f, 1f), Tooltip("Share of a CALM cell's fauna goal picks that go for the Ark in a Crystal " +
                                                "Run (hostile cells use the Ark escort tier's share). 0 = calm cells leave " +
                                                "the Ark alone.")]
        float raidCalmLureShare = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("Below this share of its hull, the Ark loses banked crystals one by one. " +
                                                "0 = it never does.")]
        float raidHullLeakBelow = 0.3f;

        [SerializeField, Min(0.1f), Tooltip("Seconds between two lost crystals while the hull is below the leak line.")]
        float raidHullLeakSeconds = 1.5f;

        [SerializeField, Min(1f), Tooltip("On the way home, every cell's lure share is multiplied by this (calm cells " +
                                          "start from the calm share), and the fauna's assaults may come in any cell.")]
        float homewardLureFactor = 2f;

        [SerializeField, Min(0f), Tooltip("On the way home, seconds between two waves of raiders in the open water " +
                                          "between cells. 0 = no raiders.")]
        float homewardRaiderSeconds = 12f;

        [SerializeField, Min(0), Tooltip("Raiders in one wave.")]
        int homewardRaiderCount = 2;

        [SerializeField, Min(0), Tooltip("No new wave while this many raiders are alive.")]
        int homewardRaiderMax = 6;

        [SerializeField, Min(1f), Tooltip("A raider's body is this many times its species' size.")]
        float homewardRaiderScale = 1.8f;

        [SerializeField, Min(50f), Tooltip("How far from the Ark, u, a raider comes in (ahead and to the side).")]
        float homewardRaiderDistance = 260f;

        [SerializeField, Tooltip("Raiders come in the open water between cells on the way OUT of a Crystal Run too, not " +
                                 "only on the way home (Shombith 2026-11-17: \"more attacks outside the cell\").")]
        bool raidersOutbound = true;

        [Header("Voyage - kaiju between the cells (Shombith 2026-11-17)")]
        [SerializeField, Min(0), Tooltip("Kaiju waiting in the open water on each crossing between two cells: a huge grazer " +
                                         "that comes for the Ark's hull. Never while the Ark is anchored. 0 = no kaiju.")]
        int kaijuPerCrossing = 1;

        [SerializeField, Min(0), Tooltip("No new kaiju while this many are alive.")]
        int kaijuMax = 2;

        [SerializeField, Min(1f), Tooltip("A kaiju's body is this many times its species' size (\"pretty huge\").")]
        float kaijuScale = 12f;

        [SerializeField, Min(0f), Tooltip("The crossing (and its kaiju) starts this far, u, before the Ark leaves the cell " +
                                          "behind it, so the kaiju is already waiting in the gap when the Ark comes out.")]
        float kaijuLeadDistance = 900f;

        [SerializeField, Min(0f), Tooltip("How far off the Ark's line, u, a kaiju waits in the gap.")]
        float kaijuSideOffset = 300f;

        [Header("Voyage - the cells")]
        [SerializeField, Range(1f, 4f), Tooltip("A voyage cell's membrane and nucleus are this many times their authored " +
                                                "size (Shombith 2026-11-16: 50% bigger; 2026-11-17: still not big enough, " +
                                                "2.5). The cell's own environment mass keeps its authored size.")]
        float cellScale = 2.5f;

        [SerializeField, Min(0f), Tooltip("Open water between two neighbouring voyage cells' membranes, u (Shombith " +
                                          "2026-11-17: \"the distances between them smaller\"). 0 = the corridor's " +
                                          "authored spacing grown with the cells, which left 1200 u at 1.5x.")]
        float cellGap = 400f;

        [SerializeField, Tooltip("A voyage cell's life (and so its crystals, the hearts) is born all through its open " +
                                 "water instead of on one feeding ground near the core (Cell.ScatterLifeSpawns).")]
        bool scatterLifeSpawns = true;

        [Header("Voyage - the galaxy map (the Voyage window's cards)")]
        [SerializeField, Tooltip("The course a card's job sails, drawn over the map.")]
        Color routeColor = new(0.70f, 1f, 0.35f, 1f);

        [SerializeField, Tooltip("Where the squad is now.")]
        Color hereColor = new(1f, 0.78f, 0.25f, 1f);

        [SerializeField, Tooltip("A port's (and a harbour's) marker colour.")]
        Color portColor = new(0.96f, 0.94f, 0.86f, 1f);

        [SerializeField, Tooltip("A vault field's marker colour.")]
        Color vaultColor = new(0.55f, 0.75f, 1f, 1f);

        [SerializeField, Tooltip("A core's marker colour.")]
        Color coreColor = new(1f, 0.45f, 0.35f, 1f);

        [SerializeField, Tooltip("A lair's (and a pen's) marker colour.")]
        Color lairColor = new(1f, 0.3f, 0.55f, 1f);

        [SerializeField, Tooltip("A nursery's marker colour.")]
        Color nurseryColor = new(0.6f, 1f, 0.6f, 1f);

        [SerializeField, Tooltip("A shrine's (and a derelict's, a hoard's) marker colour.")]
        Color shrineColor = new(0.9f, 0.85f, 1f, 1f);

        [SerializeField, Tooltip("A place this crew has not found yet, where the map shows one at all.")]
        Color unchartedColor = new(0.5f, 0.5f, 0.5f, 0.6f);

        [SerializeField, Tooltip("A lane's colour.")]
        Color laneColor = new(1f, 1f, 1f, 0.35f);

        public string DisplayName => displayName;
        public string Description => description;
        public Color AccentColor => accentColor;

        public ArkwaySettingsSO WithArk => withArk;
        public VoyageContentSO Content => content ? content : VoyageContentSO.Load();
        public ArkEscortSO ArkEscort => arkEscort;
        // The map's colours are the worn colour set's (ColorTrace): the Voyage window draws them on its canvas.
        public Color RouteColor => ColorTrace.Retrace(ColorTrace.Ui, routeColor);
        public Color HereColor => ColorTrace.Retrace(ColorTrace.Ui, hereColor);
        public float SeedElementLevel => seedElementLevel;
        public string StarterSquadName => starterSquadName;
        public IReadOnlyList<SquadSlot> StarterSeats => starterSeats;

        /// <summary>
        /// True when <paramref name="job"/> may set sail: jobs are not locked, or its template is one of
        /// <see cref="openJobs"/>. False for no job.
        /// </summary>
        public bool IsJobOpen(VoyageJob job)
        {
            if (job == null) return false;
            if (!lockJobs) return true;
            if (openJobs == null) return false;
            foreach (var id in openJobs)
                if (!string.IsNullOrEmpty(id) && id == job.TemplateId) return true;
            return false;
        }

        /// <summary><see cref="IsJobOpen"/> on the shipped settings; with no settings asset every job is open.</summary>
        public static bool JobSails(VoyageJob job)
        {
            var settings = Load();
            return job != null && (!settings || settings.IsJobOpen(job));
        }

        public float WingmateLeash => wingmateLeash;
        public float FindDistance => findDistance;
        public float VaultRadius => vaultRadius;
        public int VaultPrisms => vaultPrisms;
        public Vector3 VaultPrismScale => vaultPrismScale;
        public float VaultOpenShare => vaultOpenShare;
        public float HoldRadius => holdRadius;
        public float CullRadius => cullRadius;
        public float ObserveRadius => observeRadius;
        public float RivalAbeam => rivalAbeam;
        public int EscortCrystalsPerDifficulty => escortCrystalsPerDifficulty;
        public Element EscortRewardElement => escortRewardElement;
        public int ScorePerCrystal => scorePerCrystal;
        public int ScorePerKill => scorePerKill;
        public int ScorePerGuard => scorePerGuard;
        public FMODUnity.EventReference ToastEventSound => toastEventSound;
        public FMODUnity.EventReference ToastThreatSound => toastThreatSound;
        public FMODUnity.EventReference ToastGoodSound => toastGoodSound;
        public FMODUnity.EventReference ScoreboardSound => scoreboardSound;
        public FMODUnity.EventReference ArkHornSound => arkHornSound;
        public float RaidAnchorHostileSeconds => raidAnchorHostileSeconds;
        public float RaidAnchorCalmSeconds => raidAnchorCalmSeconds;
        public float RaidAnchorWarnSeconds => raidAnchorWarnSeconds;
        public bool RaidCrystalsAtRisk => raidCrystalsAtRisk;
        public float RaidBankRadius => raidBankRadius;
        public float RaidSpillCooldownSeconds => raidSpillCooldownSeconds;
        public float RaidCalmLureShare => raidCalmLureShare;
        public float RaidHullLeakBelow => raidHullLeakBelow;
        public float RaidHullLeakSeconds => raidHullLeakSeconds;
        public float HomewardLureFactor => homewardLureFactor;
        public float HomewardRaiderSeconds => homewardRaiderSeconds;
        public int HomewardRaiderCount => homewardRaiderCount;
        public int HomewardRaiderMax => homewardRaiderMax;
        public float HomewardRaiderScale => homewardRaiderScale;
        public float HomewardRaiderDistance => homewardRaiderDistance;
        public bool RaidersOutbound => raidersOutbound;
        public int KaijuPerCrossing => kaijuPerCrossing;
        public int KaijuMax => kaijuMax;
        public float KaijuScale => kaijuScale;
        public float KaijuLeadDistance => kaijuLeadDistance;
        public float KaijuSideOffset => kaijuSideOffset;
        public float CellScale => cellScale;
        public float CellGap => cellGap;
        public bool ScatterLifeSpawns => scatterLifeSpawns;
        public Color PortColor => ColorTrace.Retrace(ColorTrace.Ui, portColor);
        public Color VaultColor => ColorTrace.Retrace(ColorTrace.Ui, vaultColor);
        public Color CoreColor => ColorTrace.Retrace(ColorTrace.Ui, coreColor);
        public Color LairColor => ColorTrace.Retrace(ColorTrace.Ui, lairColor);
        public Color NurseryColor => ColorTrace.Retrace(ColorTrace.Ui, nurseryColor);
        public Color ShrineColor => ColorTrace.Retrace(ColorTrace.Ui, shrineColor);
        public Color UnchartedColor => ColorTrace.Retrace(ColorTrace.Ui, unchartedColor);
        public Color LaneColor => laneColor;

        /// <summary>The Ark's settings for a voyage, the corridor's worlds taken from the content's cell pool when it
        /// names any.</summary>
        public ArkwayConfig BuildArkConfig(VoyageContentSO voyage)
        {
            string label = voyage ? voyage.Lines.VoyageName : DisplayName;
            var cfg = withArk ? withArk.BuildConfig(label) : new ArkwayConfig { DisplayName = label };
            if (voyage && voyage.CellPool is { Count: > 0 }) cfg.Cells = voyage.CellPool;
            cfg.CellScale = Mathf.Max(1f, cellScale);
            cfg.CellGap = Mathf.Max(0f, cellGap);
            cfg.ScatterLifeSpawns = scatterLifeSpawns;
            return cfg;
        }
    }
}
