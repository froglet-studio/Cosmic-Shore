using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Railrush scoring - first DOMAIN to the hit target wins, on <see cref="ScoringMetric.CombatPoints"/>.
    /// One SPIKE HIT - a chain spike reaching a rival Urchin's hull - pays <see cref="spikeHitPoints"/>.
    ///
    /// <para><b>A spike hit is a BULLET-class hit, and that is not a choice this rule makes.</b> The Urchin's spike
    /// projectiles carry <c>UrchinSpikeProjectileImpactContainer</c>, whose ship effect
    /// (<c>VesselCombatHitBySpike</c>, a <c>VesselCombatHitByProjectileEffectSO</c>) reports every hull contact as
    /// <see cref="CombatHitClass.Bullet"/> through <c>CombatHitDrain.TryAdmit</c> (one per victim per 0.12 s), so
    /// every spike hit is already COUNTED (<c>BulletHitsLanded</c>) in every mode; this rule is the one that PAYS
    /// for it. Every other class pays <see cref="otherHitPoints"/> (0): the Urchin has no other way to hit a pilot,
    /// and a rule that paid for one would hide a mis-authored hull behind a working scoreboard.</para>
    ///
    /// Golf-timed like Jackstraw: the winning domain's pilots carry their finish time, losers a sentinel encoding
    /// their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Railrush", fileName = "RailrushScoringRule")]
    public class RailrushScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one SPIKE HIT - a chain spike reaching a rival hull (a Bullet-class combat hit, " +
                 "admitted once per victim per the spike effect's sameVictimCooldownSeconds).")]
        [Min(0)] [SerializeField] int spikeHitPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the spikes score in Railrush.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = hits their domain still needed.")]
        [SerializeField] string hitsLeftTemplate = "{count} Hits Left";

        [Tooltip("Every pilot's breakdown column. {count} = the spike hits they landed.")]
        [SerializeField] string hitsLandedTemplate = "{count} hits";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "RAIL TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the hits their domain still needed).")]
        [SerializeField] string defeatLabel = "HITS LEFT";

        public int SpikeHitPoints => spikeHitPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Bullet ? spikeHitPoints : otherHitPoints;

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
                TemplateText.Fill(hitsLandedTemplate, "count", s.BulletHitsLanded.ToString()))).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal(victoryHeader, victoryLabel, (int)localStats.Score, true)
                : new ScoreReveal(defeatHeader, defeatLabel, Remaining(gameData, localStats.Domain), false);
    }
}
