using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// ONE day's challenge: a mode, an intensity, a personal objective and a time budget.
    ///
    /// <para>It is a pure VALUE derived from the UTC date - every client computes the same one
    /// from <c>DailyChallengeCatalogSO</c> with no server round trip (see
    /// <c>DailyChallengeCatalogSO.ForDate</c>). UGS Cloud Save therefore stores only the
    /// player's PROGRESS against it, never the challenge itself: a definition that had to be
    /// fetched would leave the card blank on a cold or offline launch, which is the one moment
    /// it most needs to say something.</para>
    ///
    /// <para><see cref="Metric"/> is read off the LOCAL player's own round stats, not summed by
    /// domain like a mode's own end condition - "score 30 crystals" is a personal ask, and the
    /// AI seated beside you must not be able to finish it for you.</para>
    /// </summary>
    [Serializable]
    public struct DailyChallenge
    {
        /// <summary>UTC calendar day this challenge belongs to, "yyyy-MM-dd". Empty = not resolved.</summary>
        public string DateKey;

        public GameModes GameMode;
        public int Intensity;

        /// <summary>Which per-player stat the objective counts.</summary>
        public ScoringMetric Metric;

        /// <summary>How much of <see cref="Metric"/> the local player must reach.</summary>
        public int TargetValue;

        /// <summary>Seconds the player has from the turn starting. 0 = no time limit.</summary>
        public float TimeLimitSeconds;

        /// <summary>Player-facing objective line, e.g. "Collect 30 crystals in 1:00".</summary>
        public string ObjectiveText;

        /// <summary>False for <c>default</c> - no catalog, empty pool, or the date never resolved.</summary>
        public bool IsValid => !string.IsNullOrEmpty(DateKey) && TargetValue > 0;
    }
}
