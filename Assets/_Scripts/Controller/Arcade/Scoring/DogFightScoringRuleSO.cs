using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Dog Fight: the gun duel. The turn ends (<c>DogFightPointTurnMonitor</c>) when an active
    /// domain's summed <see cref="IRoundStats.CombatPoints"/> reaches
    /// <see cref="GameDataSO.CombatPointTargetCount"/>.
    ///
    /// <b>This is the rule that owns the point values</b>, and that is the whole reason
    /// <see cref="ScoringRuleSO.PointsForCombatHit"/> exists. The platform counts landed hits as
    /// raw facts (<see cref="IRoundStats.BulletHitsLanded"/> /
    /// <see cref="IRoundStats.MissileHitsLanded"/>) and has no opinion about what one is worth;
    /// this asset says a bullet is 1 and a rocket is 50, and <c>CombatHitScoring.Credit</c>
    /// applies that server-side at the instant of the hit. Every other mode's rule returns 0, so
    /// gunnery is counted everywhere and scored only here.
    ///
    /// <b>Weighting once, at the hit, rather than in the metric reader</b> is what keeps
    /// <see cref="ScoringMetric.CombatPoints"/> a plain cumulative int: it sums by domain, drives
    /// the HUD, feeds the comeback system, and orders the scoreboard through exactly the same
    /// shared machinery as crystals or prisms, with no per-metric weighting table anywhere.
    ///
    /// <b>A TEAM race, like every other mode here.</b> Points pool per domain - 2 players is a
    /// 1v1, 4 players on two domains is a 2v2 - which is also the only shape the impact layer
    /// can express: <c>Projectile.DisallowImpactOnVessel</c> refuses own-domain contact, so
    /// teammates cannot shoot each other and a same-domain pair with an individual win condition
    /// would simply be unable to fight. Domains ARE the sides here, not a scoring convenience -
    /// and Wildlife Liberation already tried and reverted the individual winner on the weaker
    /// version of this same argument (four seats, three domains).
    ///
    /// Golf-timed like every race in this family: the winning domain's pilots carry their finish
    /// time, everyone else a <see cref="GolfScoreSentinels"/> sentinel encoding their team's
    /// remaining points, so lower is better and the winners always sort first.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Dog Fight", fileName = "DogFightScoringRule")]
    public class DogFightScoringRuleSO : ScoringRuleSO
    {
        [Header("Hit values")]
        [Tooltip("Points for one landed bullet - a direct full-auto round on an opposing hull.")]
        [Min(0)] [SerializeField] int bulletPoints = 1;

        [Tooltip("Points for one landed missile - a direct skyburst strike OR being caught in " +
                 "its blast. Deliberately the SAME for both: 'hit by the missile or caught in " +
                 "the blast radius' is one event, and the shared VesselCombatHitLatch window is " +
                 "what stops a clean centre-punch (which is both) paying twice.")]
        [Min(0)] [SerializeField] int missilePoints = 50;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Missile ? missilePoints : bulletPoints;

        protected override int TargetCount(GameDataSO gameData) => gameData.CombatPointTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0,
                // or the match would finish on the countdown.
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
            {
                if (stats == null) continue;
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
            }
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual points order
            // teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Points Left",
                // The secondary line is the BREAKDOWN, not the metric: two pilots on the same
                // points got there very differently, and in a gun duel that is the interesting
                // half of the story.
                $"{LiveMetric(s)} pts · {s.BulletHitsLanded}×● {s.MissileHitsLanded}×◆")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "DOGFIGHT TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "POINTS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
