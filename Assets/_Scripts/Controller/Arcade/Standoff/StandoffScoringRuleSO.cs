using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Standoff scoring - first DOMAIN to the blast target wins, on <see cref="ScoringMetric.CombatPoints"/>.
    /// One BLAST HIT - a rival pilot caught in a blast of yours (a trigger bomb or the charged cannon) - pays
    /// <see cref="blastPoints"/>.
    ///
    /// <para><b>A blast hit is a MISSILE-BLAST-class hit, and that is not a choice this rule makes.</b> Every Grizzly
    /// blast is an <c>AOEGrizzlyExplosion</c>, whose container (<c>GrizzlyExplosionImpactorDataContainer</c>) carries
    /// <c>VesselCombatHitByGrizzlyBlast</c>: it admits the hit (one per victim per 0.5 s), takes the petals, silences
    /// the victim and raises the hit as <see cref="CombatHitClass.MissileBlast"/>, so it is COUNTED
    /// (<c>MissileHitsLanded</c>) in every mode. This rule is the one that PAYS for it. Every other class pays
    /// <see cref="otherHitPoints"/> (0): the Grizzly's other weapons (the plasma claw burns prisms, the sniper round
    /// reports no hit) would otherwise hide a mis-authored hull behind a working scoreboard.</para>
    ///
    /// Golf-timed like Corona and Kelpwood: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Standoff", fileName = "StandoffScoringRule")]
    public class StandoffScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one BLAST HIT - a rival pilot caught in one of your blasts (a MissileBlast-class combat " +
                 "hit, admitted once per victim per 0.5 s).")]
        [Min(0)] [SerializeField] int blastPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only blasts score in Standoff.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = blast hits their domain still needed.")]
        [SerializeField] string blastsLeftTemplate = "{count} Blasts Left";

        [Tooltip("Every pilot's breakdown column. {count} = the blast hits they landed.")]
        [SerializeField] string blastsLandedTemplate = "{count} blasts";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "STANDOFF TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the blast hits their domain still needed).")]
        [SerializeField] string defeatLabel = "BLASTS LEFT";

        public int BlastPoints => blastPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.MissileBlast ? blastPoints : otherHitPoints;

        protected override int TargetCount(GameDataSO gameData) => gameData.CombatPointTargetCount;

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

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .Where(s => s != null)
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.CombatPoints)
                .ThenBy(s => s.Name, System.StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : TemplateText.Fill(blastsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(blastsLandedTemplate, "count", s.MissileHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
