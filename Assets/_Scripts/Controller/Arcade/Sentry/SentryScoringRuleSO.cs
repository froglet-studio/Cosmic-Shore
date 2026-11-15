using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sentry scoring - first DOMAIN to the zap target wins, on <see cref="ScoringMetric.CombatPoints"/>. One ZAP -
    /// a Meerkat's sentinel striking a rival hull inside its guard - pays <see cref="zapPoints"/>.
    ///
    /// <para><b>A zap is a BULLET-class hit, and that is not a choice this rule makes.</b> The Meerkat's
    /// <c>MeerkatSolitonKit.ZapVessel</c> admits every zap through <c>CombatHitDrain.TryAdmit</c> as
    /// <see cref="CombatHitClass.Bullet"/> (<c>zapCooldownSeconds</c> 0, so each one a warded or silenced gate lets
    /// through) and raises it on the combat-hit channel, so every zap is already COUNTED
    /// (<c>BulletHitsLanded</c>) in every mode; this rule is the one that PAYS for it. Every other class pays
    /// <see cref="otherHitPoints"/> (0): the Meerkat has no other way to hit a pilot, and a rule that paid for one
    /// would hide a mis-authored hull behind a working scoreboard.</para>
    ///
    /// Golf-timed like Jackstraw (its shape, with the class and the counter swapped): the winning domain's pilots
    /// carry their finish time, losers a sentinel encoding their domain's deficit. Every line the player reads is an
    /// authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Sentry", fileName = "SentryScoringRule")]
    public class SentryScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one ZAP - a sentinel striking a rival hull inside its guard (a Bullet-class combat hit, " +
                 "about five a second while a rival stays in the guard).")]
        [Min(0)] [SerializeField] int zapPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the sentinels score in Sentry.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = zaps their domain still needed.")]
        [SerializeField] string zapsLeftTemplate = "{count} Zaps Left";

        [Tooltip("Every pilot's breakdown column. {count} = the zaps their sentinels landed.")]
        [SerializeField] string zapsLandedTemplate = "{count} zaps";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "SENTRY TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the zaps their domain still needed).")]
        [SerializeField] string defeatLabel = "ZAPS LEFT";

        public int ZapPoints => zapPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Bullet ? zapPoints : otherHitPoints;

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
                    : TemplateText.Fill(zapsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(zapsLandedTemplate, "count", s.BulletHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
