using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Liberation: the hunt is a DOMAIN RACE, like every other multiplayer mode here.
    /// The turn ends (<c>WildlifeKillTurnMonitor</c>) when an active domain's summed
    /// <see cref="IRoundStats.LifeformsKilled"/> reaches
    /// <see cref="GameDataSO.LifeformTargetCount"/>, and that domain wins.
    ///
    /// > **This shipped once as a FREE-FOR-ALL (first player to the target) and was reverted.**
    /// > Do not re-derive it. The mode seats up to FOUR players but the platform has only THREE
    /// > playable domains (`GameDataSO.ActiveDomains` = Jade / Ruby / Gold - Blue is the "no
    /// > team" sentinel), so a four-player lobby ALWAYS has two players sharing a colour. A
    /// > per-individual winner therefore bypasses the domain machinery every other mode runs on:
    /// > the winner banner, the domain HUD panels, the scoreboard's team ordering and the
    /// > placement-order fold all speak in domains, and a mode that answers "a player won" leaves
    /// > every one of them describing something that is not the result. Teammates sharing a total
    /// > is the intended shape, not a defect to work around.
    ///
    /// So this rule is deliberately THIN - it picks the metric and the target and inherits the
    /// rest of <see cref="ScoringRuleSO"/>'s domain behaviour (winner = highest domain sum, ties
    /// by <c>ActiveDomains</c> order so every machine agrees; remaining = the domain's deficit).
    /// Only the presentation is its own.
    ///
    /// Golf-timed like HexRace / Scurry / Rampage / Ribcage: the winning DOMAIN's players carry
    /// their finish time, everyone else the shared <see cref="GolfScoreSentinels"/> encoding of
    /// their team's deficit, so lower is better and the winners always sort first.
    ///
    /// A note on what does and does not score, because the ecology is a live system and not a
    /// pile of targets: only PLAYER-ATTRIBUTED kills count. A creature that starves, or that a
    /// shark eats, credits nobody - <c>Fauna.Die</c> filters engine attribution before it
    /// publishes, and <c>StatsManager.LifeformKilled</c> only credits a name on the player
    /// roster. So the swarm dying of its own accord can never move a scoreboard, and a team
    /// cannot farm the food web instead of hunting.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Wildlife Liberation",
        fileName = "WildlifeLiberationScoringRule")]
    public class WildlifeLiberationScoringRuleSO : ScoringRuleSO
    {
        protected override int TargetCount(GameDataSO gameData) => gameData.LifeformTargetCount;

        /// <summary>
        /// Identical in shape to <see cref="RibcageScoringRuleSO"/> and
        /// <see cref="RampageScoringRuleSO"/>: the first active domain whose summed kills reach
        /// the target wins. <see cref="ScoringRuleSO.ResolveWinner"/> and
        /// <see cref="ScoringRuleSO.Remaining"/> are inherited unchanged - both already read
        /// <c>ScoringMetrics.SumByDomain</c> over this rule's metric.
        /// </summary>
        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the turn would finish the instant the first creature dies.
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

        /// <summary>
        /// The winning DOMAIN's players carry the finish time; everyone else the sentinel
        /// encoding their own team's remaining kills, so teammates on a losing domain tie.
        /// </summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null) continue;
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeHexRaceLoserScore(Remaining(gameData, stats.Domain));
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual kills order
            // teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.LifeformsKilled)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Kills Left",
                $"{LiveMetric(s)} Kills")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "HUNT TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "KILLS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
