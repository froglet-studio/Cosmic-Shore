using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Astro League: points (not golf). The metric is per-player GoalsScored aggregated by
    /// domain; <see cref="IsObjectiveReached"/> implements the mercy rule (a domain's goal sum
    /// reaching <see cref="GameDataSO.GoalTargetCount"/> ends the match early). Full-time and
    /// golden-goal endings are decided by <see cref="AstroLeagueController"/>, which then uses
    /// <see cref="ScoringRuleSO.ResolveWinner"/> / <see cref="AssignScores"/> from this rule for
    /// the shared end-game flow. Each player's Score IS their personal goal tally.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/AstroLeague", fileName = "AstroLeagueScoringRule")]
    public class AstroLeagueScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.GoalTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target > 0)
            {
                int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
                for (int i = 0; i < dc; i++)
                {
                    var d = GameDataSO.ActiveDomains[i];
                    if (ScoringMetrics.SumByDomain(gameData, metric, d) >= target)
                    {
                        winner = d;
                        return true;
                    }
                }
            }
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.GoalsScored;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Winning-domain players rank above losers at equal personal goal counts.
            var ordered = gameData.RoundStatsList
                .OrderByDescending(s => s.Domain == gameData.WinnerDomain ? 1 : 0)
                .ThenByDescending(s => s.Score);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Goal{((int)s.Score == 1 ? "" : "s")}",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} GOAL{(diff == 1 ? "" : "S")}",
                (int)localStats.Score,
                false);
        }
    }
}
