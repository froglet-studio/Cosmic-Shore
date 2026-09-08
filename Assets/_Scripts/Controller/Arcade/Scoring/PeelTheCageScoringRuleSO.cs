using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// PeelTheCage: the race is DESTRUCTION. The turn ends (<c>PeelTheCagePrismTurnMonitor</c>) when an
    /// active domain's summed <see cref="IRoundStats.HostilePrismsDestroyed"/> reaches
    /// <see cref="GameDataSO.PrismTargetCount"/> (authored via FrogletTools ▸ Game Modes ▸ End
    /// Game Conditions). Winning-domain players score their FINISH TIME; losing players a
    /// sentinel encoding their team's remaining prisms. Golf-timed like SkimRace/Scurry/Rampage.
    ///
    /// Scoring mass is everything that is not your own team's laid trail: the cage (environment
    /// mass, non-roster owner ⇒ StatsManager classifies it hostile whatever colour it wears),
    /// rival trails, AND fauna bodies. Your own and your teammates' trails never score, so there
    /// is no lay-and-smash farming loop.
    ///
    /// NOTE on the ecology's role under this metric (chosen deliberately, 2026-08): because the
    /// fauna are not rostered attackers, a creature eating a player's trail credits nobody, so
    /// the swarm does not directly move anyone's score the way it would if the metric were a
    /// live standing-mass stock. It is still not decoration - it is an OBSTACLE that costs the
    /// trailing teams time at the bone, and its multi-prism bodies are themselves hostile mass
    /// worth points to whoever kills them. Do not "fix" this by switching the metric without
    /// asking; the trade was made with eyes open.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/PeelTheCage", fileName = "PeelTheCageScoringRule")]
    public class PeelTheCageScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.PrismTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the turn would finish the instant the first bar shatters.
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
            // single source of truth; the "SkimRace" naming is legacy - the encoding is shared).
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual prisms destroyed order
            // teammates; name is the final tiebreak so every peer builds an identical list.
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
                ? new ScoreReveal("VICTORY", "BREAKOUT TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "PRISMS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
