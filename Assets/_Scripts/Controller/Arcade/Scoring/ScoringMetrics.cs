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
            ScoringMetric.SwitchesThreaded  => stats.SwitchesThreaded,
            _                               => 0,
        };

        /// <summary>
        /// The BEST single player's reading of the chosen metric on <paramref name="domain"/>,
        /// or 0 when that domain fielded nobody.
        ///
        /// <para>The sibling fold to <see cref="SumByDomain"/>, and the right one whenever every
        /// pilot is working on the SAME objective rather than contributing to a shared pile.
        /// Switchback is the case that needed it: all pilots fly one course, so a domain's
        /// progress is its lead runner's - under a sum a two-pilot domain would show 2x the
        /// course, reach the target at half the gates, and hand the win to whoever had more
        /// teammates.</para>
        ///
        /// <para>Reached through <see cref="ScoringRuleSO.DomainValue"/> rather than called
        /// directly by consumers, so a mode picks its fold in ONE place and the HUD boxes, the
        /// end condition, the placement order and the comeback deficit cannot disagree about
        /// what a domain's score is.</para>
        /// </summary>
        public static int BestByDomain(GameDataSO gameData, ScoringMetric metric, Domains domain)
        {
            int best = 0;
            var list = gameData.RoundStatsList;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                var stats = list[i];
                if (stats == null || stats.Domain != domain) continue;
                int v = Read(stats, metric);
                if (v > best) best = v;
            }
            return best;
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
