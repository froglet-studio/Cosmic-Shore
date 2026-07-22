using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal Capture: golf-timed like HexRace. Shares <c>NetworkCrystalCollisionTurnMonitor</c>
    /// with HexRace, so the turn ends when an active domain's summed crystals reach the target;
    /// the winning domain is the highest crystal sum. Winning-domain players score their FINISH
    /// TIME (the end-game score they display); losing players a sentinel encoding their team's
    /// remaining crystals (golf: lower is better), so the scoreboard shows the winners' time and
    /// each losing team's crystals left (individual crystals collected on the secondary line).
    /// Ordering stays TEAM-major by construction: every finish time sits below every loser
    /// sentinel, and loser sentinels order teams by how close they got.
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
            // Same sentinel scheme as every time-based golf mode (GolfScoreSentinels is the
            // single source of truth; the "HexRace" naming is legacy - the encoding is shared).
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeHexRaceLoserScore(Remaining(gameData, stats.Domain));
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual crystals order
            // teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CrystalsCollected)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

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
                ? new ScoreReveal("VICTORY", "CAPTURE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "CRYSTALS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
