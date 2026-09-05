using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

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
            // The one FLOAT-backed metric: rounded once, here, so every downstream consumer
            // (the per-domain NetworkVariable sum, the HUD column, the goal row, the
            // scoreboard secondary) keeps the single int contract the rest of them share.
            ScoringMetric.VolumeDestroyed   => Mathf.RoundToInt(stats.HostileVolumeDestroyed),
            _                               => 0,
        };

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
