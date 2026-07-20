using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz: co-op-vs-environment, golf. Mid-turn, every player's Score is their
    /// blitz composite (hostile volume + weighted kills + weighted crystals, fed
    /// server-side by WildlifeBlitzScoreKeeper) and the TEAM's summed composite races the
    /// cell's CellEndGameScore threshold (published into
    /// <see cref="GameDataSO.GoalTargetCount"/> by WildlifeBlitzObjectiveTurnMonitor).
    /// <see cref="IsObjectiveReached"/> fires when the team sum reaches the target - the
    /// cell is cleared and every teammate's Score becomes the shared clear time; if the
    /// clock runs out first the controller ends the game with a Blue (no-winner) DNF and
    /// every teammate gets the DNF sentinel.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/WildlifeBlitz", fileName = "WildlifeBlitzScoringRule")]
    public class WildlifeBlitzScoringRuleSO : ScoringRuleSO
    {
        public const float DnfScore = 999f;

        protected override int TargetCount(GameDataSO gameData) => gameData.GoalTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            int target = TargetCount(gameData);
            if (target <= 0) return false;

            float teamSum = 0f;
            IRoundStats first = null;
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                first ??= stats;
                teamSum += stats.Score;
            }

            if (first == null || teamSum < target) return false;

            // Co-op: the whole roster shares one domain (DomainAssigner's co-op path).
            winner = first.Domain;
            return true;
        }

        public override int Remaining(GameDataSO gameData, Domains domain)
        {
            // The blitz objective is the TEAM Score sum, not a per-domain metric.
            float teamSum = 0f;
            foreach (var stats in gameData.RoundStatsList)
                if (stats != null) teamSum += stats.Score;
            return Mathf.Max(0, TargetCount(gameData) - (int)teamSum);
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            // Win: the whole team shares the clear time. Loss (Blue): everyone DNFs.
            float finalScore = winner != Domains.Blue ? finishTime : DnfScore;
            foreach (var stats in gameData.RoundStatsList)
                if (stats != null) stats.Score = finalScore;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf: finish time ascending; DNF sorts behind any real clear time.
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => s.Score)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                s.Score < DnfScore ? ScoreResultBuilder.FormatTime(s.Score) : "DNF",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            return didWin
                ? new ScoreReveal("VICTORY", "CLEAR TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "CELL UNCLEARED", 0, false);
        }
    }
}
