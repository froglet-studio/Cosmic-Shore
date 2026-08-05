using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// HexRace: golf-timed crystal race. The first active domain whose summed crystals reach the
    /// target wins together; winners score their finish time, losers a sentinel encoding the
    /// team's remaining crystals (golf: lower is better). Target keeps HexRace's historical
    /// "&gt; 0 ? configured : 39" fallback.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/HexRace", fileName = "HexRaceScoringRule")]
    public class HexRaceScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) =>
            gameData.CrystalTargetCount > 0 ? gameData.CrystalTargetCount : 39;

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
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeHexRaceLoserScore(Remaining(gameData, stats.Domain));
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CrystalsCollected);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Crystals Left",
                $"{LiveMetric(s)} Crystals")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "RACE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "CRYSTALS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
