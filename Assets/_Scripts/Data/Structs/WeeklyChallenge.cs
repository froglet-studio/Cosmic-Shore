using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// ONE day's challenge: a mode, an intensity, a personal objective and a time budget.
    ///
    /// <para>It is a pure VALUE derived from the UTC date - every client computes the same one
    /// from <c>WeeklyChallengeCatalogSO</c> with no server round trip (see
    /// <c>WeeklyChallengeCatalogSO.ForDate</c>). UGS Cloud Save therefore stores only the
    /// player's PROGRESS against it, never the challenge itself: a definition that had to be
    /// fetched would leave the card blank on a cold or offline launch, which is the one moment
    /// it most needs to say something.</para>
    ///
    /// <para><see cref="Metric"/> is read off the LOCAL player's own round stats, not summed by
    /// domain like a mode's own end condition - "score 30 crystals" is a personal ask, and the
    /// AI seated beside you must not be able to finish it for you.</para>
    /// </summary>
    [Serializable]
    public struct WeeklyChallenge
    {
        /// <summary>UTC calendar day this challenge belongs to, "yyyy-MM-dd". Empty = not resolved.</summary>
        public string PeriodKey;

        public GameModes GameMode;
        public int Intensity;

        /// <summary>
        /// The domain the player flies for this challenge. Pinned like the intensity, because a
        /// weekly challenge is a fixed ask rather than a lobby - and because the run seats the
        /// card's minimum, so the colour is not a team decision anyone else is party to.
        /// Defaults to <see cref="Domains.Jade"/>, which is also what the menu resets every
        /// player to on spawn.
        /// </summary>
        public Domains Domain;

        /// <summary>Which per-player stat the objective counts.</summary>
        public ScoringMetric Metric;

        /// <summary>How much of <see cref="Metric"/> the local player must reach. 0 when
        /// <see cref="UsesModeTarget"/> - the number is the live match's, not the catalog's.</summary>
        public int TargetValue;

        /// <summary>
        /// The target is the MODE'S OWN end condition, resolved at run time from the match
        /// (<c>ScoringRuleSO.TargetFor</c>), and completion is also granted when the player's
        /// domain wins the race. Guarantees the ask is exactly what the game itself races to.
        /// </summary>
        public bool UsesModeTarget;

        /// <summary>Player-facing objective line, e.g. "Collect 30 crystals". No duration: the run
        /// plays the mode's own end conditions and has no clock of its own.</summary>
        public string ObjectiveText;

        /// <summary>False for <c>default</c> - no catalog, empty pool, or the date never resolved.</summary>
        public bool IsValid => !string.IsNullOrEmpty(PeriodKey) && (TargetValue > 0 || UsesModeTarget);
    }
}
