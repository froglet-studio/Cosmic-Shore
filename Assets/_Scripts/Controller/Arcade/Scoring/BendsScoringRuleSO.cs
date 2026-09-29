using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Bends: the Dolphin-only debuff duel. The turn ends
    /// (<see cref="BendsPointTurnMonitor"/>) when an active domain's summed
    /// <see cref="IRoundStats.CombatPoints"/> reaches
    /// <see cref="GameDataSO.CombatPointTargetCount"/>.
    ///
    /// <b>This rule is the whole reason the mode can exist without a new metric.</b> The platform
    /// already counts landed vessel-vs-vessel hits by CLASS as raw facts
    /// (<see cref="IRoundStats.BulletHitsLanded"/> / <see cref="IRoundStats.MissileHitsLanded"/> /
    /// <see cref="IRoundStats.DebuffHitsLanded"/>) and has no opinion about what one is worth;
    /// Dog Fight's asset says a bullet is 1 and a rocket 50, and this one says a BEND is 10 and
    /// guns are worth nothing at all. <c>CombatHitScoring.Credit</c> applies whichever rule is
    /// live, server-side, at the instant of the hit.
    ///
    /// <b>Why guns pay zero rather than simply never happening.</b> The Dolphin carries no guns,
    /// so in a well-formed match the bullet and missile branches are unreachable - but the mode's
    /// vessel restriction is data (the <c>Vessels</c> list on ArcadeGameBends), and a rule that
    /// silently paid for gunnery would turn a mis-authored roster into a scoring bug rather than a
    /// roster bug. Zero says what the mode means: the only thing worth points here is bending
    /// somebody.
    ///
    /// <b>A TEAM race</b>, like every other mode in this family, and for the same structural
    /// reason: <c>ExplosionImpactor.AcceptImpactee</c> refuses own-domain vessels unless the blast
    /// authors friendly fire, so you cannot bend a teammate at all. Domains ARE the sides. (See
    /// <see cref="DogFightScoringRuleSO"/> for the longer version of this argument, including
    /// Wildlife Liberation's reverted free-for-all.)
    ///
    /// Golf-timed like every race here: the winning domain's pilots carry their finish time,
    /// everyone else a <see cref="GolfScoreSentinels"/> sentinel encoding their team's remaining
    /// points, so lower is better and the winners always sort first.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/The Bends", fileName = "BendsScoringRule")]
    public class BendsScoringRuleSO : ScoringRuleSO
    {
        [Header("Hit values")]
        [Tooltip("Points for one BEND - an opposing pilot caught in your Dolphin crystal blast " +
                 "and stripped of element levels. Counted once per blast per victim through the " +
                 "shared VesselCombatHitLatch window, so a cone that grows through a pilot over " +
                 "several frames pays once. A blast that catches two enemies pays twice.\n\n" +
                 "ONE, deliberately: the mode races to 3, so a point IS a bend and the HUD " +
                 "number is the thing that happened rather than a scaled proxy for it - the " +
                 "same shape as Joust, which races to 3 collisions.")]
        [Min(0)] [SerializeField] int bendPoints = 1;

        [Tooltip("Points for landed GUNNERY. Zero on purpose: the Dolphin has no guns, and a " +
                 "rule that paid for them would hide a mis-authored vessel roster behind a " +
                 "working scoreboard. Left authorable so the mode can be opened to a mixed " +
                 "roster deliberately rather than by accident.")]
        [Min(0)] [SerializeField] int gunneryPoints = 0;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Debuff ? bendPoints : gunneryPoints;

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
                    : $"{Remaining(gameData, s.Domain)} Bends Left",
                    // The secondary line is the BEND COUNT, not the points. At the shipped weight
                // (1 point per bend) the two are the same number, so printing both would just
                // read as "2 pts · 2 bends"; printing the raw count keeps the line honest if
                // bendPoints is ever re-tuned away from 1.
                $"{s.DebuffHitsLanded} bends")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "BEND TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "BENDS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
