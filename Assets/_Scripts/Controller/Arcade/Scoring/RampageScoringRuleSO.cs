using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage: points (not golf) - the destructive analog of Crystal Capture ("Scurry").
    /// The turn ends (<c>RampagePrismTurnMonitor</c>) when an active domain's summed
    /// <see cref="IRoundStats.HostilePrismsDestroyed"/> reaches the target
    /// (<see cref="GameDataSO.PrismTargetCount"/>, authored via Tools &gt; Cosmic Shore &gt;
    /// End Game Conditions); the winning domain is the highest destruction sum. Golf-timed
    /// like HexRace/Crystal Capture: winning-domain players score their FINISH TIME (the
    /// end-game score they display); losing players a sentinel encoding their team's remaining
    /// prisms, so the scoreboard shows the winners' time and each losing team's prisms left
    /// (individual prisms smashed on the secondary line). Scoring mass = ALL environment
    /// mass (flora/fauna, any color - StatsManager classifies non-roster-owned prisms hostile)
    /// plus OTHER domains' player-laid trails; your own/teammates' trails never score (the
    /// roster domain check filters them), so lay-and-shatter farming is worthless by
    /// construction.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Rampage", fileName = "RampageScoringRule")]
    public class RampageScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.PrismTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the turn would finish the instant the first prism shatters.
                winner = Domains.Blue;
                return false;
            }

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
            // sentinel, loser sentinels ordered by team deficit. Individual prisms smashed
            // order teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.HostilePrismsDestroyed)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Prisms Left",
                $"{LiveMetric(s)} Prisms")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "RAMPAGE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
