using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Joust: golf-timed jousting. The turn ends when an active domain's summed joust collisions
    /// reach the target; the winning domain (highest sum) scores elapsed time, losers a flat
    /// sentinel. Loser deficits are the DOMAIN gap (not an individual's), which is what fixes
    /// BUGS.md B2 once the reveal reads this rule's results.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Joust", fileName = "JoustScoringRule")]
    public class JoustScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.JoustTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
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
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.Domain == winner ? finishTime : GolfScoreSentinels.JoustLoserScore;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.JoustCollisions);

            var rows = ordered.Select(s =>
            {
                int joustsLeft = Remaining(gameData, s.Domain);
                return new ScoreResultBuilder.Row(
                    s.Name,
                    s.Domain,
                    s.Score,
                    GolfScoreSentinels.IsFinishTime(s.Score)
                        ? ScoreResultBuilder.FormatTime(s.Score)
                        : $"{joustsLeft} Joust{(joustsLeft == 1 ? "" : "s")} Left",
                    $"{LiveMetric(s)} Jousts");
            }).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return didWin
                ? new ScoreReveal("VICTORY", $"WON BY {diff} JOUST{(diff == 1 ? "" : "S")}", Mathf.FloorToInt(localStats.Score), true)
                : new ScoreReveal("DEFEAT", $"LOST BY {diff} JOUST{(diff == 1 ? "" : "S")}", Remaining(gameData, localStats.Domain), false);
        }
    }
}
