using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manta Sprawl: time-boxed territory land-grab. Points (not golf) keyed on the Blocks
    /// metric (mass laid). The <c>NetworkTimeBasedTurnMonitor</c> ends the turn on the clock;
    /// the winning domain is the highest summed <c>BlocksCreated</c>. Each player's score IS
    /// their blocks created — higher is better, no secondary stat line.
    ///
    /// STATELESS: the asset is a shared singleton, so this is a pure function of the
    /// <see cref="GameDataSO"/> passed in (see <see cref="ScoringRuleSO"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/MantaSprawl", fileName = "MantaSprawlScoringRule")]
    public class MantaSprawlScoringRuleSO : ScoringRuleSO
    {
        // No early objective — the time monitor ends the turn. IsObjectiveReached therefore
        // always reports "not yet", so the turn runs the full duration.
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            // Score IS the territory you laid (the metric). Domain aggregation in
            // CalculateDomainStats determines team standing; winner arg is unused here.
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.BlocksCreated;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList.OrderByDescending(s => s.Score);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Territory",
                null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} TERRITORY",
                (int)localStats.Score,
                false);
        }
    }
}
