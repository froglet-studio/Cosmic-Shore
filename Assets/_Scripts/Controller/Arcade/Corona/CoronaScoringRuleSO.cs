using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Corona scoring - first DOMAIN to the bloom target wins, on <see cref="ScoringMetric.CombatPoints"/>.
    /// One BLOOM HIT - a rival pilot caught in one of your Manta bombs' blooms - pays <see cref="bloomPoints"/>.
    ///
    /// <para><b>A bloom hit is a DEBUFF-class hit, and that is not a choice this rule makes.</b> Every bloom a Manta
    /// sets off (a cashed Kabloom, a fuse that ran out, a hull that touched a prism) is an <c>AOEMantaBloom</c> whose
    /// container carries <c>VesselCombatHitByCrystalBlast</c>: it admits the hit through
    /// <c>CombatHitDrain.TryAdmit</c> (one per victim per second), takes the petals, silences the victim and raises
    /// the hit as <see cref="CombatHitClass.Debuff"/>, so it is COUNTED (<c>DebuffHitsLanded</c>) in every mode. This
    /// rule is the one that PAYS for it. Every other class pays <see cref="otherHitPoints"/> (0): the Manta has no other
    /// way to hit a pilot, and a rule that paid for one would hide a mis-authored hull behind a working scoreboard.</para>
    ///
    /// Golf-timed like Jackstraw and Kelpwood: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Corona", fileName = "CoronaScoringRule")]
    public class CoronaScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one BLOOM HIT - a rival pilot caught in a bloom of a bomb you planted (a Debuff-class " +
                 "combat hit, admitted once per victim per second).")]
        [Min(0)] [SerializeField] int bloomPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only blooms score in Corona.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = blooms their domain still needed.")]
        [SerializeField] string bloomsLeftTemplate = "{count} Blooms Left";

        [Tooltip("Every pilot's breakdown column. {count} = the bloom hits they landed.")]
        [SerializeField] string bloomsLandedTemplate = "{count} blooms";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "CORONA TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the blooms their domain still needed).")]
        [SerializeField] string defeatLabel = "BLOOMS LEFT";

        public int BloomPoints => bloomPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Debuff ? bloomPoints : otherHitPoints;

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
                    : TemplateText.Fill(bloomsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(bloomsLandedTemplate, "count", s.DebuffHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
