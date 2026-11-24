using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pod Hunt scoring - first DOMAIN to the blast target wins, on <see cref="ScoringMetric.CombatPoints"/>. One BLAST -
    /// a gunner's flown missile or the bomb seat's bomb bursting on a rival hull - pays <see cref="blastPoints"/>.
    ///
    /// <para><b>A blast is a MISSILE-class hit, and that is not a choice this rule makes.</b> The Orca's missile burst is
    /// <c>AOEExplosion.prefab</c> (its container carries <c>VesselCombatHitByMissileBlast</c>) and its bomb is
    /// <c>AOEGrizzlyExplosion.prefab</c> (<c>VesselCombatHitByGrizzlyBlast</c>); both report
    /// <see cref="CombatHitClass.MissileBlast"/> through <c>CombatHitDrain.TryAdmit</c>, so both are already COUNTED
    /// (<c>MissileHitsLanded</c>) in every mode. This rule is the one that PAYS for them. The other two missile tiers pay
    /// the same, so a future change of tier still scores.</para>
    ///
    /// <para><b>The guns pay nothing</b> (<see cref="gunHitPoints"/>, 0): the sniper's strip is a Debuff-class hit and the
    /// fast gun's rounds Bullet-class, and Pod Hunt is a missile hunt. They still matter - the sniper is the one gun that
    /// breaks a super-shielded shield, and the fast gun clears the wreckage a missile has to come round.</para>
    ///
    /// Golf-timed like Jackstraw and Kelpwood: the winning domain's pilots carry their finish time, losers a sentinel
    /// encoding their domain's deficit. Every line the player reads is an authored template.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Pod Hunt", fileName = "PodHuntScoringRule")]
    public class PodHuntScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one BLAST - a flown missile or the bomb bursting on a rival hull (a missile-class combat hit, " +
                 "admitted once per victim per sameVictimCooldownSeconds on the blast's vessel effect).")]
        [Min(0)] [SerializeField] int blastPoints = 1;

        [Tooltip("Points for a GUN hit (the sniper's Debuff-class strip, the fast gun's Bullet-class rounds) and any other " +
                 "class. Zero on purpose: Pod Hunt is won with missiles and bombs.")]
        [Min(0)] [SerializeField] int gunHitPoints = 0;

        [Header("Results text (authored; empty shows nothing)")]
        [Tooltip("A losing pilot's score column. {count} = blasts their domain still needed.")]
        [SerializeField] string blastsLeftTemplate = "{count} Blasts Left";

        [Tooltip("Every pilot's breakdown column. {count} = the blasts their crew landed.")]
        [SerializeField] string blastsLandedTemplate = "{count} blasts";

        [Tooltip("The reveal's header when the local pilot's domain won.")]
        [SerializeField] string victoryHeader = "VICTORY";

        [Tooltip("The reveal's label under a win (the value is the finish time).")]
        [SerializeField] string victoryLabel = "HUNT TIME";

        [Tooltip("The reveal's header when the local pilot's domain lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";

        [Tooltip("The reveal's label under a loss (the value is the blasts their domain still needed).")]
        [SerializeField] string defeatLabel = "BLASTS LEFT";

        public int BlastPoints => blastPoints;

        /// <summary>An exhaustive price list (Dog Fight's reason): a class added later is worth the gun rate, never a
        /// blast's.</summary>
        public override int PointsForCombatHit(CombatHitClass hitClass) => hitClass switch
        {
            CombatHitClass.MissileShockwave => blastPoints,
            CombatHitClass.MissileBlast     => blastPoints,
            CombatHitClass.MissileDirect    => blastPoints,
            _                               => gunHitPoints,
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
