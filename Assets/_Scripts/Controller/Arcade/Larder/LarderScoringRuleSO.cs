using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Larder scoring - first DOMAIN to the missile target wins, on <see cref="ScoringMetric.CombatPoints"/>. One
    /// MISSILE HIT - a missile from a Shrike's plants reaching a rival hull - pays <see cref="missilePoints"/>.
    ///
    /// <para><b>A missile hit is a MISSILE-class hit, and that is not a choice this rule makes.</b> The Shrike's
    /// <c>ShrikeExecutor.Detonate</c> admits a rival hull inside a missile's blast through <c>CombatHitDrain.TryAdmit</c>
    /// with its config's <c>missileHitClass</c> (MissileBlast) and raises it on the combat-hit channel, so it is COUNTED
    /// (<c>MissileHitsLanded</c>) in every mode. This rule PAYS every missile class
    /// (<see cref="CombatHitClasses.IsMissile"/>), so a re-classed missile still scores. Every other class pays
    /// <see cref="otherHitPoints"/> (0): the Void Lance's hull hit is the Shrike's Space verb, and Larder is its Charge
    /// game.</para>
    ///
    /// Golf-timed like Jackstraw, Kelpwood and Corona: the winning domain's pilots carry their finish time, losers a
    /// sentinel encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Larder", fileName = "LarderScoringRule")]
    public class LarderScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one MISSILE HIT - a missile from your plants reaching a rival hull (any missile-class combat " +
                 "hit).")]
        [Min(0)] [SerializeField] int missilePoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only missiles score in Larder.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = missile hits their domain still needed.")]
        [SerializeField] string hitsLeftTemplate = "{count} Hits Left";

        [Tooltip("Every pilot's breakdown column. {count} = the missile hits they landed.")]
        [SerializeField] string hitsLandedTemplate = "{count} missiles";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "LARDER TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the hits their domain still needed).")]
        [SerializeField] string defeatLabel = "HITS LEFT";

        public int MissilePoints => missilePoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            CombatHitClasses.IsMissile(hitClass) ? missilePoints : otherHitPoints;

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
                    : TemplateText.Fill(hitsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(hitsLandedTemplate, "count", s.MissileHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
