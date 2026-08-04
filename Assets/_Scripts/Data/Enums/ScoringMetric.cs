namespace CosmicShore.Data
{
    /// <summary>
    /// The single per-player stat a scoring rule aggregates by Domain. One metric drives a
    /// mode's HUD readout, its "remaining" counter, its end condition, and its scoreboard
    /// secondary line - so the number players watch can never diverge from the number that
    /// ends the game. Selected per mode on the mode's <c>ScoringRuleSO</c> asset.
    /// Always assign explicit values to avoid Unity serialization drift.
    /// </summary>
    public enum ScoringMetric
    {
        Crystals = 0,
        OmniCrystals = 1,
        ElementalCrystals = 2,
        Jousts = 3,
        Goals = 4,
        // Rampage: hostile prisms destroyed (reads IRoundStats.HostilePrismsDestroyed).
        // "Hostile" = everything except your own/your teammates' PLAYER-LAID mass:
        // other domains' trails, plus ALL environment mass (flora/fauna carry
        // non-roster owner names, so StatsManager classifies them hostile regardless
        // of color). Laying-then-shattering your own trail is worthless by
        // construction - trails ARE rostered, so the domain check filters them.
        PrismsDestroyed = 5,
        // Ribcage: prisms you currently have STANDING (reads IRoundStats.PrismsRemaining -
        // incremented when you lay a prism, decremented when ANYTHING destroys it, including
        // a rival's ram and a fauna's bite). A LIVE stock, not a cumulative total, and that
        // distinction is the mode: a cumulative "prisms created" counter can only go up, so
        // nothing that eats your mass could ever set you back and the fauna would be pure
        // decoration. With this metric the swarm chewing your trail directly un-scores you.
        PrismsRemaining = 6,
    }
}
