using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drumfire: points, not golf - the most VOLUME torn out of the drum wins.
    ///
    /// <para>The only shipping rule whose objective is never REACHED: Drumfire ends on the
    /// clock (<see cref="DrumfireTimeTurnMonitor"/>), so <see cref="IsObjectiveReached"/>
    /// always answers false and <see cref="TargetCount"/> is 0. Everything else the platform
    /// asks a rule for still works off the shared machinery -
    /// <see cref="ScoringRuleSO.ResolveWinner"/> picks the highest domain sum when the
    /// controller stops the match, and <see cref="ScoringRuleSO.ResolvePlacementOrder"/> folds
    /// the same sums into Maelstrom standings.</para>
    ///
    /// <para>The metric is <see cref="ScoringMetric.VolumeDestroyed"/> - hostile volume, so
    /// every prism in the drum counts for every domain (the drum is laid in
    /// <see cref="Domains.Blue"/>, which <c>StatsManager.IsFriendlyEnvironmentPrism</c> treats
    /// as hostile to all) and a pilot's own trail never does. VOLUME rather than a prism COUNT
    /// because the drum is built of panes of one size but braced with heavier structure: a shot
    /// that takes out a rib is worth more than the same number of skin panes, which is the
    /// aiming lesson stated in the score.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Drumfire", fileName = "DrumfireScoringRule")]
    public class DrumfireScoringRuleSO : ScoringRuleSO
    {
        /// <summary>
        /// No race target - the clock is the end condition. Reported as 0 so the goal row draws
        /// the objective and its running count with no denominator, which is the honest readout
        /// for a mode you cannot "finish".
        /// </summary>
        protected override int TargetCount(GameDataSO gameData) => 0;

        /// <summary>
        /// Always false: only <see cref="DrumfireTimeTurnMonitor"/> ends a Drumfire turn. A rule
        /// that answered true here would race the clock and hand the win to whoever happened to
        /// cross an invented threshold first.
        /// </summary>
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            return false;
        }

        /// <summary>
        /// Each pilot scores the volume THEY tore out. No sentinel encoding: this is a points
        /// mode, so the raw metric is already the ranking, teammates are ordered by their own
        /// contribution, and the winning DOMAIN is the sum (resolved by
        /// <see cref="ScoringRuleSO.ResolveWinner"/>).
        /// </summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = LiveMetric(stats);
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Score descending, then prisms smashed, then name - so every peer builds an
            // identical list from the replicated stats.
            var ordered = gameData.RoundStatsList
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score:N0} Volume",
                $"{s.HostilePrismsDestroyed} Prisms")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff:N0} VOLUME",
                (int)localStats.Score,
                false);
        }
    }
}
