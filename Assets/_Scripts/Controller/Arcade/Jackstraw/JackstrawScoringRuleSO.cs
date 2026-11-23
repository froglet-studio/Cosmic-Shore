using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Jackstraw scoring - first DOMAIN to the strike target wins, on <see cref="ScoringMetric.CombatPoints"/>.
    /// One STRIKE - the Thresher's ball or any link of its chain reaching a rival hull - pays
    /// <see cref="strikePoints"/>.
    ///
    /// <para><b>A strike is a STRIKE-class hit, and that is not a choice this rule makes.</b> The Thresher's
    /// <c>ThresherExecutor.TryHitVessel</c> reports every hull contact as <see cref="CombatHitClass.Strike"/>
    /// through <c>CombatHitDrain.TryAdmit</c> (one per victim per <c>vesselHitCooldownSeconds</c>, 1 s), so every
    /// strike is already COUNTED (<c>StrikeHitsLanded</c>) in every mode; this rule is the one that PAYS for it.
    /// Every other class pays <see cref="otherHitPoints"/> (0): the Thresher has no other way to hit a pilot, and
    /// a rule that paid for one would hide a mis-authored hull behind a working scoreboard.</para>
    ///
    /// Golf-timed like Serpent Gulag: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Jackstraw", fileName = "JackstrawScoringRule")]
    public class JackstrawScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one STRIKE - the Thresher's ball or chain reaching a rival hull (a Strike-class combat " +
                 "hit, admitted once per victim per vesselHitCooldownSeconds).")]
        [Min(0)] [SerializeField] int strikePoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the flail scores in Jackstraw.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = strikes their domain still needed.")]
        [SerializeField] string strikesLeftTemplate = "{count} Strikes Left";

        [Tooltip("Every pilot's breakdown column. {count} = the strikes they landed.")]
        [SerializeField] string strikesLandedTemplate = "{count} strikes";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "HEAP TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the strikes their domain still needed).")]
        [SerializeField] string defeatLabel = "STRIKES LEFT";

        public int StrikePoints => strikePoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Strike ? strikePoints : otherHitPoints;

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
                    : TemplateText.Fill(strikesLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(strikesLandedTemplate, "count", s.StrikeHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
