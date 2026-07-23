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
        /// <summary>
        /// RETIRED (2026-07-21): was the standalone Freestyle sandbox's live feed; that
        /// game was deleted (freestyle IS the Menu_Main lava lamp). No rule asset selects
        /// this metric and ScoringMetrics has no arm for it. Member kept for
        /// serialized-int stability - do not reuse.
        /// </summary>
        VolumeCreated = 5,
        /// <summary>
        /// RETIRED (2026-07-21): was the CellularDuel volume-churn composite
        /// (created + hostile destroyed + friendly destroyed); that mode was deleted.
        /// No rule asset selects this metric and ScoringMetrics has no arm for it.
        /// Member kept for serialized-int stability - do not reuse.
        /// </summary>
        VolumeActivity = 6,
    }
}
