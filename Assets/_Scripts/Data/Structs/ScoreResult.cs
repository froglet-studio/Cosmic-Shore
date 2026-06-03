namespace CosmicShore.Data
{
    /// <summary>
    /// One row of final, ranked scoring results — the single source of truth for
    /// "who placed where" that every end-game surface (scoreboard banner + cards,
    /// end-game cinematic, crystal reward) reads. Produced once by the game mode
    /// (server-side in networked modes, locally in single-player) and, in networked
    /// modes, assembled identically on each client from the already-synced score
    /// arrays. See Docs/ScoringSystem/REFACTOR.md R10.
    /// </summary>
    public readonly struct ScoreResult
    {
        /// <summary>1-based placement after sorting (1 = best).</summary>
        public readonly int Rank;

        public readonly string Name;

        public readonly Domains Domain;

        /// <summary>The mode's primary score value (golf rules: lower is better).</summary>
        public readonly float Score;

        /// <summary>
        /// Mode-formatted secondary display line (e.g. "12 Crystals", "3 Jousts Left",
        /// "01:24:30"). Computed once by the producing mode so the scoreboard and the
        /// end-game reveal can never disagree (this is what dissolves BUGS.md B2).
        /// </summary>
        public readonly string Secondary;

        public ScoreResult(int rank, string name, Domains domain, float score, string secondary)
        {
            Rank = rank;
            Name = name;
            Domain = domain;
            Score = score;
            Secondary = secondary;
        }
    }
}
