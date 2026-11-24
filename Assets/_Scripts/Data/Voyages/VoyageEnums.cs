namespace CosmicShore.Data
{
    // Every value is pinned (CLAUDE.md: always assign a static numeric value to an enum member). The voyage
    // record is saved, so a value is never reused once shipped.

    /// <summary>What a HyperSea location IS when a squad arrives (Docs/Voyages/VOYAGES.md §5, DEPTH.md §4).</summary>
    public enum VoyageExperience
    {
        /// <summary>A domain's home port. Safe; docking at your own extracts what is aboard.</summary>
        Harbour = 0,
        /// <summary>A neutral port: a job board and a news ledger, no extraction.</summary>
        Port = 1,
        /// <summary>Super-shielded shells with an artifact inside, opened with a payload.</summary>
        VaultField = 2,
        /// <summary>A contested cell whose nucleus a squad is asked to hold.</summary>
        Core = 3,
        /// <summary>A cell whose creatures are predators at pressure; the Kaiju lairs here when it is sighted.</summary>
        Lair = 4,
        /// <summary>The thieves' stash: sealed shells hiding what was stolen; pirates raid it.</summary>
        Hoard = 5,
        /// <summary>A bloom cell, rich in crystals: harvested, studied and tended.</summary>
        Nursery = 6,
        /// <summary>A stranded Ark with a crew aboard: first responders stand by it until its crew is clear.</summary>
        Derelict = 7,
        /// <summary>Fauna held behind another domain's mass: activists raze the pen.</summary>
        Pen = 8,
        /// <summary>A beacon the priests keep vigils at; sagas end here.</summary>
        Shrine = 9,
    }

    /// <summary>
    /// The kind of work a pilot drums up at port for their faction type (VOYAGES.md §6.2). The TYPE is what decides
    /// which work a pilot brings; the faction itself (one of many of its type) is <see cref="FactionCatalog"/>'s.
    /// 5 onward (2026-11-07) complete the VOYAGES.md §6.2 table so every pilot can name their faction's type; since
    /// DEPTH.md §4 every type brings work.
    /// </summary>
    public enum FactionType
    {
        None = 0,
        Merchant = 1,
        Hunter = 2,
        Aristocrat = 3,
        Pirate = 4,
        BountyHunter = 5,
        Explorer = 6,
        Activist = 7,
        Thief = 8,
        Gatherer = 9,
        Contractor = 10,
        Sport = 11,
        Researcher = 12,
        Preservationist = 13,
        FirstResponder = 14,
        Priest = 15,
        Diplomat = 16,
    }

    /// <summary>What a job is called: its flavour on the card and in the story. The WORK it asks is its <see cref="WorkVerb"/>.</summary>
    public enum JobKind
    {
        /// <summary>Arrive at a port with the Ark standing; the dispatch aboard reaches that port's news.</summary>
        Courier = 0,
        /// <summary>Open a vault at a vault field and stow its artifact in the Ark.</summary>
        Hunt = 1,
        /// <summary>Recover an artifact from a wreck and stow it in the Ark.</summary>
        Salvage = 2,
        /// <summary>Hold a core: keep the Ark under it until the hold is done.</summary>
        Defence = 3,
        /// <summary>Chart an uncharted place (explorers).</summary>
        Survey = 4,
        /// <summary>Gather a bloom's crystals (gatherers).</summary>
        Harvest = 5,
        /// <summary>Keep a living creature in reach for a while (researchers).</summary>
        Study = 6,
        /// <summary>Stand by a derelict until its crew is clear (first responders).</summary>
        Rescue = 7,
        /// <summary>Raze the mass that pens a cell's fauna (activists).</summary>
        Liberate = 8,
        /// <summary>Lay mass to shore a breached core (contractors).</summary>
        Build = 9,
        /// <summary>Lift a rival harbour's freshest news (thieves).</summary>
        Lift = 10,
        /// <summary>Meet a notorious crew where it stands (bounty hunters).</summary>
        Bounty = 11,
        /// <summary>A vigil at a shrine (priests).</summary>
        Rite = 12,
        /// <summary>Carry an accord to a port (diplomats).</summary>
        Parley = 13,
        /// <summary>Raid a hoard (pirates).</summary>
        Raid = 14,
        /// <summary>Tend a nursery without a kill (preservationists).</summary>
        Tend = 15,
        /// <summary>Skim the course (sport).</summary>
        Exhibition = 16,
        /// <summary>Carry cargo to a port with the hull above a floor (merchants).</summary>
        Convoy = 17,
        /// <summary>Fell the predators at a lair, or the Kaiju (hunters).</summary>
        Cull = 18,
    }

    /// <summary>What the world watches to call a job's work done (DEPTH.md §4). One switch in <c>VoyageRules.WorkDone</c>.</summary>
    public enum WorkVerb
    {
        /// <summary>The Ark reaches the destination standing.</summary>
        Arrive = 0,
        /// <summary>Goal artifacts stowed in the Ark here.</summary>
        Stow = 1,
        /// <summary>The pilot stays within the hold radius of the Ark for Goal seconds.</summary>
        Hold = 2,
        /// <summary>The squad collects Goal crystals since arrival.</summary>
        Gather = 3,
        /// <summary>Goal hostile creatures fall near the Ark.</summary>
        Cull = 4,
        /// <summary>The squad lays Goal prisms.</summary>
        Lay = 5,
        /// <summary>The squad destroys Goal volume of hostile mass.</summary>
        Raze = 6,
        /// <summary>The pilot keeps a living creature within reach for Goal seconds.</summary>
        Observe = 7,
        /// <summary>The Ark arrives while the job's rival crew stands there.</summary>
        Meet = 8,
    }

    /// <summary>The squad role a job suits best (the Guardian / Destroyer / Cultivator trio, the team's 2026-10-30 voyage meeting).</summary>
    public enum SquadRole
    {
        Any = 0,
        Guardian = 1,
        Destroyer = 2,
        Cultivator = 3,
    }

    /// <summary>How a job ended.</summary>
    public enum JobOutcome
    {
        None = 0,
        Succeeded = 1,
        /// <summary>The squad left the destination without doing the work.</summary>
        Failed = 2,
        /// <summary>The Ark fell. Everything aboard is lost or taken.</summary>
        ArkLost = 3,
    }

    /// <summary>What a piece of news is about. Each one is the seed of a job (VOYAGES.md §6.3, DEPTH.md §2-§6).</summary>
    public enum NewsTag
    {
        None = 0,
        /// <summary>Vaults are glowing at a vault field: hunters' work.</summary>
        Sighting = 1,
        /// <summary>A fallen Ark lies at a location with artifacts spilled: pirates' work.</summary>
        Wreck = 2,
        /// <summary>A core is under pressure and asks for a squad to hold it: aristocrats' work.</summary>
        Aid = 3,
        /// <summary>A nursery's bloom is ripe: gatherers' and researchers' work.</summary>
        Harvest = 4,
        /// <summary>Predators crowd a lair: hunters' work.</summary>
        Lair = 5,
        /// <summary>A chart fragment names an uncharted place: explorers' work.</summary>
        Chart = 6,
        /// <summary>A derelict's crew calls for rescue: first responders' work.</summary>
        Distress = 7,
        /// <summary>A core's shell is breached: contractors' work.</summary>
        Breach = 8,
        /// <summary>Fauna are penned behind another domain's mass: activists' work.</summary>
        Pen = 9,
        /// <summary>A price on a notorious crew: bounty hunters' work (or on this crew: a warning).</summary>
        Bounty = 10,
        /// <summary>Word of a laden Ark: a pirate's tip.</summary>
        Rumour = 11,
        /// <summary>A port asks for an accord to be carried to a crew: diplomats' work.</summary>
        Accord = 12,
        /// <summary>A hoard is stocked: pirates' work.</summary>
        Hoard = 13,
        /// <summary>A chapter of a saga, found by the pilot it names.</summary>
        Chapter = 14,
        /// <summary>A port under pressure posts a cargo contract: merchants' convoy work.</summary>
        Contract = 15,
        /// <summary>Another domain holds a core: aristocrats retake it.</summary>
        Held = 16,
        /// <summary>The Kaiju is sighted at a lair: the one lit beacon every port knows.</summary>
        Kaiju = 17,
    }

    /// <summary>Which port an event's news is written to.</summary>
    public enum NewsPlace
    {
        /// <summary>The job's destination (only when it is a port or harbour; otherwise the nearest port to it).</summary>
        Destination = 0,
        /// <summary>The port the job was taken at.</summary>
        Origin = 1,
        /// <summary>The squad's home harbour.</summary>
        Home = 2,
        /// <summary>Every port and harbour.</summary>
        EveryPort = 3,
    }

    /// <summary>Which location an event's news is about.</summary>
    public enum NewsAbout
    {
        /// <summary>The job's destination.</summary>
        Destination = 0,
        /// <summary>A vault field other than the destination, nearest first.</summary>
        AnotherVaultField = 1,
        /// <summary>A core other than the destination, nearest first.</summary>
        AnotherCore = 2,
        /// <summary>The nearest location of the write's own experience, other than the destination.</summary>
        AnotherOf = 3,
        /// <summary>The nearest place this crew has not charted yet; the write is skipped when every place is charted.</summary>
        NearestUncharted = 4,
    }

    /// <summary>What stands between the squad and a rival crew it meets (DEPTH.md §3).</summary>
    public enum RivalRelation
    {
        /// <summary>Nothing between you: they are passing through.</summary>
        None = 0,
        /// <summary>Your domain: three more wingmen.</summary>
        CoOp = 1,
        /// <summary>Another domain, after the same finds.</summary>
        Race = 2,
        /// <summary>They came for your Ark.</summary>
        Hostile = 3,
    }

    /// <summary>How a rival crew picks its work.</summary>
    public enum RivalTemper
    {
        /// <summary>The nearest work.</summary>
        Careful = 0,
        /// <summary>The roughest course.</summary>
        Bold = 1,
        /// <summary>The best pay.</summary>
        Greedy = 2,
    }
}
