using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Snap scoring - first DOMAIN to the ring-hit target wins, on <see cref="ScoringMetric.CombatPoints"/>. One RING
    /// HIT - a vortex ring the Goby &amp; Pistol fired reaching a rival hull (the pair's Charge ability, Vortex Sting) -
    /// pays <see cref="ringHitPoints"/>.
    ///
    /// <para><b>A ring hit is a MissileDirect-class hit, and that is the vessel's choice, not this rule's.</b>
    /// <c>GobyPistolDuo.ScoreRingHit</c> admits it through <c>CombatHitDrain.TryAdmit</c> with the config's
    /// <c>ringHitClass</c> (MissileDirect, one per victim per <c>hitCooldown</c>) and raises it on the combat-hit channel,
    /// so it is COUNTED (<c>MissileHitsLanded</c>) in every mode; this rule is the one that PAYS for it. Every other
    /// class pays <see cref="otherHitPoints"/> (0): the pair has no other way to hit a pilot, and a rule that paid for one
    /// would hide a mis-authored hull behind a working scoreboard. Jackstraw's shape, on the ring's class.</para>
    ///
    /// Golf-timed: the winning domain's pilots carry their finish time, losers a sentinel encoding their domain's deficit.
    /// Every line the player reads is an authored template; an empty one shows nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Snap", fileName = "SnapScoringRule")]
    public class SnapScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one RING HIT - a Goby & Pistol vortex ring reaching a rival hull (the config's ringHitClass, " +
                 "MissileDirect).")]
        [Min(0)] [SerializeField] int ringHitPoints = 1;

        [Tooltip("The combat-hit class a ring hit is reported as. Must match GobyPistolConfig.ringHitClass; the generator " +
                 "(author_snap_assets.py) fails if the two drift apart.")]
        [SerializeField] CombatHitClass ringHitClass = CombatHitClass.MissileDirect;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: only the rings score in Snap.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = ring hits their domain still needed.")]
        [SerializeField] string hitsLeftTemplate = "{count} Hits Left";

        [Tooltip("Every pilot's breakdown column. {count} = the ring hits they landed.")]
        [SerializeField] string hitsLandedTemplate = "{count} ring hits";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "SNAP TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the ring hits their domain still needed).")]
        [SerializeField] string defeatLabel = "HITS LEFT";

        public int RingHitPoints => ringHitPoints;
        public CombatHitClass RingHitClass => ringHitClass;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == ringHitClass ? ringHitPoints : otherHitPoints;

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
