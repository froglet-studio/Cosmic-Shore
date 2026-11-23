using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Flatline scoring - first DOMAIN to the point target wins, on <see cref="ScoringMetric.CombatPoints"/>. Only the
    /// Brittle Star's hull-on-hull hits score, at the fleet price (<c>CombatHitDrain.PointsFor</c>, the list
    /// Broadside pays, ten points to the petal):
    ///   • a ROUND that strikes a rival hull (<see cref="CombatHitClass.Bullet"/>) pays <see cref="roundPoints"/> (1);
    ///   • a CORD, or a thrown saber, across a rival hull (<see cref="CombatHitClass.Strike"/>) pays
    ///     <see cref="strikePoints"/> (8).
    /// Every other class pays <see cref="otherHitPoints"/> (0). <b>The ring blast scores nothing</b> (Shombith,
    /// 2026-11-09: "only the bullets and chords should score points"): it still bursts, still breaks the panes in its
    /// plane and still takes petals, because the mode never changes the vessel; it simply does not pay here.
    ///
    /// <para>Neither class is a choice this rule makes. The Brittle Star's rounds are the Sparrow's pooled round,
    /// whose impact container reports a <c>Bullet</c> (latched per victim for 0.05 s); its cords and thrown sabers
    /// report a <c>Strike</c> through <c>CombatHitDrain.TryAdmit</c> (0.5 s per victim). Counted everywhere
    /// (<c>BulletHitsLanded</c>, <c>StrikeHitsLanded</c>), scored here.</para>
    ///
    /// Golf-timed like Dustup and Hood: the winning domain's pilots carry their finish time, losers a
    /// sentinel encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Flatline", fileName = "FlatlineScoringRule")]
    public class FlatlineScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values (the fleet price; ten points to the petal)")]
        [Tooltip("Points for a ROUND that strikes a rival hull (a Bullet-class hit, latched per victim for 0.05 s).")]
        [Min(0)] [SerializeField] int roundPoints = 1;

        [Tooltip("Points for a CORD or a thrown saber across a rival hull (a Strike-class hit, once per victim per 0.5 s).")]
        [Min(0)] [SerializeField] int strikePoints = 8;

        [Tooltip("Points for any OTHER combat hit class - the ring blast among them. Zero on purpose: only the rounds " +
                 "and the cords score in Flatline.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = points their domain still needed.")]
        [SerializeField] string pointsLeftTemplate = "{count} Points Left";

        [Tooltip("Every pilot's breakdown column. {strikes} = cord and saber strikes landed, {rounds} = rounds landed.")]
        [SerializeField] string breakdownTemplate = "{strikes} strikes, {rounds} rounds";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "FLATLINE TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the points their domain still needed).")]
        [SerializeField] string defeatLabel = "POINTS LEFT";

        public int RoundPoints => roundPoints;
        public int StrikePoints => strikePoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.Bullet => roundPoints,
            CombatHitClass.Strike => strikePoints,
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
                    : TemplateText.Fill(pointsLeftTemplate, "count", Remaining(gameData, s.Domain).ToString()),
                TemplateText.Fill(breakdownTemplate,
                    "strikes", s.StrikeHitsLanded.ToString(),
                    "rounds", s.BulletHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
