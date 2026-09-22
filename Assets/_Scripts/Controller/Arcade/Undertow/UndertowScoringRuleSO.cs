using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Undertow: the Scarab-only cavitation duel. Two things score, and BOTH are things the plate
    /// does on its own, in every mode, whether or not anyone is counting:
    ///
    ///   • a BEND - an opposing pilot caught in your plate and stripped of every element for four
    ///     seconds (the plate's own debuff effect, wired since the Scarab shipped). Counted by the
    ///     platform as a Debuff-class combat hit (<see cref="IRoundStats.DebuffHitsLanded"/>) and
    ///     paid into <see cref="IRoundStats.CombatPoints"/> at <see cref="bendPoints"/> by
    ///     <c>CombatHitScoring.Credit</c>, server-side, at the instant of the hit;
    ///   • a KILL - a creature whose heart the plate reaches dies the Squirrel's joust
    ///     (<c>ExplosionWitherLifeformByCrystalEffectSO</c> on the cavitation container, added
    ///     with this mode; the plate's prism half already shredded creatures' BODIES). Counted by
    ///     the ecology as <see cref="IRoundStats.LifeformsKilled"/>.
    ///
    /// <b>The two are folded into ONE domain score by <see cref="DomainValue"/></b> - the seam
    /// Switchback added for exactly this reason: a domain's score is read in five places that
    /// must never disagree (Remaining, ResolveWinner, ResolvePlacementOrder, DomainDelta and the
    /// HUD's domain boxes), and all five read this one virtual. <see cref="metric"/> stays
    /// <see cref="ScoringMetric.CombatPoints"/>, so the goal row's icon, the per-player HUD card
    /// (<c>LiveMetric</c>, which is not virtual) and the comeback deficit all read BENDS alone;
    /// the kills ride on top in the domain fold and are shown on the scoreboard's secondary line.
    /// That asymmetry is the stated cost of scoring two stats without a new metric, and it is
    /// deliberate: the BEND is the act the mode is named for, and a creature is worth a third of
    /// a pilot so the wildlife half can shorten a match but never decide one on its own.
    ///
    /// Golf-timed like every race here: the winning domain's pilots carry their finish time,
    /// everyone else a <see cref="GolfScoreSentinels"/> sentinel encoding their team's remaining
    /// points. A TEAM race for the structural reason every mode in this family is:
    /// <c>ExplosionImpactor.AcceptImpactee</c> declines own-domain vessels, so you cannot bend
    /// a teammate at all.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Undertow", fileName = "UndertowScoringRule")]
    public class UndertowScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one BEND - an opposing pilot caught in your cavitation plate. Paid " +
                 "into CombatPoints by CombatHitScoring.Credit once per plate per victim (the " +
                 "shared VesselCombatHitLatch window). THREE, so a pilot is worth three creatures.")]
        [Min(0)] [SerializeField] int bendPoints = 3;

        [Tooltip("Points for one creature the plate KILLS. Folded into the domain score from " +
                 "LifeformsKilled (never paid into CombatPoints - that stat is bends alone). ONE.")]
        [Min(0)] [SerializeField] int killPoints = 1;

        [Tooltip("Points for landed GUNNERY. Zero on purpose: the Scarab has no guns, and a rule " +
                 "that paid for them would hide a mis-authored vessel roster behind a working " +
                 "scoreboard.")]
        [Min(0)] [SerializeField] int gunneryPoints = 0;

        public int BendPoints => bendPoints;
        public int KillPoints => killPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Debuff ? bendPoints : gunneryPoints;

        protected override int TargetCount(GameDataSO gameData) => gameData.CombatPointTargetCount;

        /// <summary>Bends (already weighted, in CombatPoints) plus kills x <see cref="killPoints"/>.</summary>
        public override int DomainValue(GameDataSO gameData, Domains domain) =>
            ScoringMetrics.SumByDomain(gameData, ScoringMetric.CombatPoints, domain)
            + killPoints * ScoringMetrics.SumByDomain(gameData, ScoringMetric.LifeformsKilled, domain);

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target <= 0)
            {
                // Target not resolved yet (monitor hasn't started / synced) - never end on 0.
                winner = Domains.Blue;
                return false;
            }

            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (DomainValue(gameData, d) >= target)
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
            {
                if (stats == null) continue;
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
            }
        }

        /// <summary>One pilot's own points: their bends (already in CombatPoints) plus their kills.</summary>
        int PlayerPoints(IRoundStats s) => s.CombatPoints + killPoints * s.LifeformsKilled;

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            // Golf order = TEAM-major by construction: finish times (winners) below every loser
            // sentinel, loser sentinels ordered by team deficit. Individual points order
            // teammates; name is the final tiebreak so every peer builds an identical list.
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(PlayerPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Points Left",
                // The breakdown, not the points: at the shipped weights "2 bends · 3 kills"
                // says what happened where "9 pts" says only how much it was worth.
                $"{s.DebuffHitsLanded} bends · {s.LifeformsKilled} kills")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "UNDERTOW TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "POINTS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
