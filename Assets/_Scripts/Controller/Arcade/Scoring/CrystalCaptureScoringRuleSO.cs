using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal Capture: points (not golf). Shares <c>NetworkCrystalCollisionTurnMonitor</c> with
    /// HexRace, so the turn ends when an active domain's summed crystals reach the target; the
    /// winning domain is the highest crystal sum. Each player's score IS their crystal count -
    /// higher is better - and there is no secondary stat line.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/CrystalCapture", fileName = "CrystalCaptureScoringRule")]
    public class CrystalCaptureScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.CrystalTargetCount;

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
                stats.Score = stats.CrystalsCollected;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList.OrderByDescending(s => s.Score);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Crystals",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} CRYSTAL{(diff == 1 ? "" : "S")}",
                (int)localStats.Score,
                false);
        }
    }
}
