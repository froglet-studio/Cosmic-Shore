using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cellular Duel (multiplayer): points (not golf). The mode is TIME-based - the
    /// 120s NetworkTimeBasedTurnMonitor ends each of the two rounds, so
    /// <see cref="IsObjectiveReached"/> never fires; the winner at final-round end is the
    /// active domain with the highest summed volume activity (created + hostile destroyed
    /// + friendly destroyed - the same three stats the legacy tracker's strategies summed
    /// at multiplier 1). Each player's Score IS their composite volume, cumulative across
    /// both rounds. Results rank TEAM-major, then individual score, then name (peer-stable).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/CellularDuel", fileName = "CellularDuelScoringRule")]
    public class CellularDuelScoringRuleSO : ScoringRuleSO
    {
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            // Time-based mode: turns end on the clock, never on a metric target.
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = LiveMetric(stats);
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var placement = ResolvePlacementOrder(gameData);
            var ordered = gameData.RoundStatsList
                .OrderBy(s => { int i = placement.IndexOf(s.Domain); return i < 0 ? int.MaxValue : i; })
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            // Bare int score - matches DuelForCellScoreboard.FormatPlayerScore so the
            // Results path renders identically to the legacy per-player path it replaces.
            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                ((int)s.Score).ToString(),
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} VOLUME",
                (int)localStats.Score,
                false);
        }
    }
}
