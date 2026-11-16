using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Marionette scoring - first DOMAIN to the strike target wins, on <see cref="ScoringMetric.CombatPoints"/>. A
    /// STRIKE - a possessed creature's attack landing on a rival hull - pays <see cref="strikePoints"/>; a BURST - the
    /// Charge 5 upgrade, leaving a creature so it bursts on every rival vessel near it - pays <see cref="burstPoints"/>.
    ///
    /// <para><b>The classes are the wasp's, not choices this rule makes.</b> <c>JewelWaspExecutor.Strike</c> admits the
    /// victim through <c>CombatHitDrain.TryAdmit</c> with its config's <c>attackHitClass</c> (Strike) for the attack and
    /// <c>burstHitClass</c> (MissileBlast) for the burst, and raises both on the combat-hit channel, so they are COUNTED
    /// in every mode; this rule PAYS them. Every other class pays <see cref="otherHitPoints"/> (0).</para>
    ///
    /// Golf-timed like Larder and Kelpwood: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Marionette", fileName = "MarionetteScoringRule")]
    public class MarionetteScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one STRIKE - a possessed creature's attack landing on a rival hull (CombatHitClass.Strike).")]
        [Min(0)] [SerializeField] int strikePoints = 1;

        [Tooltip("Points for one BURST hit - leaving a creature with Charge 5 bursts it on every rival vessel near it " +
                 "(CombatHitClass.MissileBlast, the wasp's burstHitClass).")]
        [Min(0)] [SerializeField] int burstPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the puppet's attack and its burst score.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = hits their domain still needed.")]
        [SerializeField] string hitsLeftTemplate = "{count} Hits Left";

        [Tooltip("Every pilot's breakdown column. {count} = the points they landed (strikes and bursts).")]
        [SerializeField] string hitsLandedTemplate = "{count} strikes";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "PUPPET TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the hits their domain still needed).")]
        [SerializeField] string defeatLabel = "HITS LEFT";

        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Strike => strikePoints,
            CombatHitClass.MissileBlast => burstPoints,
            _ => otherHitPoints,
        };

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
                TemplateText.Fill(hitsLandedTemplate, "count", s.CombatPoints.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
