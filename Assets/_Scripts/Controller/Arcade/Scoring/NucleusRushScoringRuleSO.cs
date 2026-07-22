using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Nucleus Rush ("Brood Rush"): points, not golf. Every fauna wave the cell births
    /// under a domain's nucleus claim scores that domain one brood (tracked on a
    /// representative player's <c>GoalsScored</c>, aggregated by domain like Astro
    /// League goals). The turn ends when an active domain's brood sum reaches
    /// <see cref="GameDataSO.GoalTargetCount"/> (default 3, authored via
    /// Tools &gt; Cosmic Shore &gt; End Game Conditions) - a race to N waves.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/NucleusRush", fileName = "NucleusRushScoringRule")]
    public class NucleusRushScoringRuleSO : ScoringRuleSO
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
            var ordered = gameData.RoundStatsList.OrderByDescending(s => s.Score);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Brood{((int)s.Score == 1 ? "" : "s")}",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} BROOD{(diff == 1 ? "" : "S")}",
                (int)localStats.Score,
                false);
        }
    }
}
