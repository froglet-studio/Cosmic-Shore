namespace CosmicShore.Data
{
    // Every value is pinned (CLAUDE.md: always assign a static numeric value to an enum member). The voyage
    // record is saved, so a value is never reused once shipped.

    /// <summary>What a HyperSea location IS when a squad arrives (Docs/Voyages/VOYAGES.md §5).</summary>
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
    }

    /// <summary>
    /// The kind of work a pilot drums up at port for their faction type (VOYAGES.md §6.2). Factions themselves wait
    /// (Garrett, 2026-10-28); the TYPE is what decides which work a pilot brings.
    /// </summary>
    public enum FactionType
    {
        None = 0,
        Merchant = 1,
        Hunter = 2,
        Aristocrat = 3,
        Pirate = 4,
    }

    /// <summary>What a job asks the squad to do at its destination.</summary>
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

    /// <summary>What a piece of news is about. Each one is the seed of a job (VOYAGES.md §6.3).</summary>
    public enum NewsTag
    {
        None = 0,
        /// <summary>Vaults are glowing at a vault field: hunters' work.</summary>
        Sighting = 1,
        /// <summary>A fallen Ark lies at a location with artifacts spilled: pirates' work.</summary>
        Wreck = 2,
        /// <summary>A core is under pressure and asks for a squad to hold it: aristocrats' work.</summary>
        Aid = 3,
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
    }
}
