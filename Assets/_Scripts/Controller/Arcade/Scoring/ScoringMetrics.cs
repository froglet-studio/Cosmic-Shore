using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stateless reader for the per-player scoring metric plus a per-domain summer.
    /// One metric-parameterized helper (<c>SumByDomain</c>) that every <see cref="ScoringRuleSO"/>
    /// uses, so a mode picks its metric in one place - supersedes the old per-metric
    /// <c>GameDataSO.Sum…ByDomain</c> helpers (only <c>SumCrystalsCollectedByDomain</c> remains, for
    /// <c>ElementalComebackSystem</c>).
    /// </summary>
    public static class ScoringMetrics
    {
        /// <summary>Reads the chosen metric off a single player's round stats.</summary>
        public static int Read(IRoundStats stats, ScoringMetric metric) => metric switch
        {
            ScoringMetric.Crystals          => stats.CrystalsCollected,
            ScoringMetric.OmniCrystals      => stats.OmniCrystalsCollected,
            ScoringMetric.ElementalCrystals => stats.ElementalCrystalsCollected,
            ScoringMetric.Jousts            => stats.JoustCollisions,
            ScoringMetric.Goals             => stats.GoalsScored,
            ScoringMetric.PrismsDestroyed   => stats.HostilePrismsDestroyed,
            ScoringMetric.PrismsRemaining   => stats.PrismsRemaining,
            ScoringMetric.LifeformsKilled   => stats.LifeformsKilled,
            ScoringMetric.CombatPoints      => stats.CombatPoints,
            _                               => 0,
        };

        /// <summary>
        /// Writes the chosen metric back onto a single player's round stats - the inverse of
        /// <see cref="Read"/>. Used by the shared final-results broadcast so every peer's stats
        /// carry the server's authoritative metric values without per-mode array plumbing.
        /// </summary>
        public static void Write(IRoundStats stats, ScoringMetric metric, int value)
        {
            switch (metric)
            {
                case ScoringMetric.Crystals:          stats.CrystalsCollected = value; break;
                case ScoringMetric.OmniCrystals:      stats.OmniCrystalsCollected = value; break;
                case ScoringMetric.ElementalCrystals: stats.ElementalCrystalsCollected = value; break;
                case ScoringMetric.Jousts:            stats.JoustCollisions = value; break;
                case ScoringMetric.Goals:             stats.GoalsScored = value; break;
                case ScoringMetric.PrismsDestroyed:   stats.HostilePrismsDestroyed = value; break;
            }
        }

        /// <summary>Sums the chosen metric across every player on <paramref name="domain"/>.</summary>
        public static int SumByDomain(GameDataSO gameData, ScoringMetric metric, Domains domain)
        {
            int sum = 0;
            var list = gameData.RoundStatsList;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                var stats = list[i];
                if (stats != null && stats.Domain == domain)
                    sum += Read(stats, metric);
            }
            return sum;
        }
    }
}
