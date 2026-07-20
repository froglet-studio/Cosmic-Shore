using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz (single-player scene): golf. Mid-turn, the local player's Score is
    /// the blitz composite (hostile volume + weighted kills + weighted crystals, fed by
    /// WildlifeBlitzScoreKeeper) racing the cell's CellEndGameScore threshold
    /// (SingleplayerWildlifeBlitzTurnMonitor). At game end the LOCAL player's Score
    /// becomes their finish time on a win or the DNF sentinel on a loss - the convention
    /// carried by AssignScores: a non-Blue winner means the local player won and
    /// finishTime is their clear time; Blue means the blitz timed out.
    /// The end condition is the monitor's Score-threshold check, so
    /// <see cref="IsObjectiveReached"/> never fires.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/WildlifeBlitz", fileName = "WildlifeBlitzScoringRule")]
    public class WildlifeBlitzScoringRuleSO : ScoringRuleSO
    {
        public const float DnfScore = 999f;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            // The SP blitz monitor ends the turn on the Score threshold (win) or the
            // clock (loss); the rule only formats the outcome.
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            var local = gameData.LocalRoundStats;
            if (local == null) return;
            local.Score = winner != Domains.Blue ? finishTime : DnfScore;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf: finish time ascending; DNF (and any AI composite scores, which are
            // never finalized in the solo blitz) sort behind a real clear time.
            var ordered = gameData.RoundStatsList
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
