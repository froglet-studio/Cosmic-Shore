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
        /// <summary>Trail volume the player laid (truncated to int). Freestyle's live feed.</summary>
        VolumeCreated = 5,
        /// <summary>
        /// Total volume churn the player caused: created + hostile destroyed + friendly
        /// destroyed (truncated to int) - the CellularDuel-family composite (the legacy
        /// tracker summed the same three strategies at multiplier 1). Composite metrics
        /// cannot be written back through <c>ScoringMetrics.Write</c>; their components
        /// each replicate via their own RoundStats NetworkVariables.
        /// </summary>
        VolumeActivity = 6,
    }
}
