using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Dustup scoring - first DOMAIN to the point target wins, on
    /// <see cref="ScoringMetric.CombatPoints"/>. One DUSTING - an opposing Butterfly passing
    /// through your Scale Dust - pays <see cref="dustPoints"/>.
    ///
    /// <para><b>A dusting is a STRIKE, and that is not a choice this rule makes.</b> The Butterfly's
    /// dust capsule already reports its vessel contacts as <see cref="CombatHitClass.Strike"/>
    /// (<c>ButterflyCombatHitBySkimmerEffect</c>, on the dust container since the hull shipped), so
    /// every Butterfly has been COUNTED dusting rivals in every mode; this rule is the one that
    /// PAYS for it. Counted everywhere, scored once - the Dog Fight / Bends / Broadside split.</para>
    ///
    /// <para><b>One per PASS, not per second.</b> A skimmer dispatches its vessel effects on ENTRY
    /// (<c>SkimmerImpactor</c>'s stay handler runs no effects), so hovering over a rival scores the
    /// pass that caught them and nothing after it - you have to break off and come round again,
    /// which on the fleet's slowest-turning hull is the whole skill. The shared
    /// <c>VesselCombatHitLatch</c> window (1 s on the Butterfly's reporter) absorbs a rival
    /// bouncing in and out of the capsule's edge.</para>
    ///
    /// Golf-timed like Undertow: the winning domain's pilots carry their finish time, losers a
    /// sentinel encoding their domain's deficit.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Dustup", fileName = "DustupScoringRule")]
    public class DustupScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one DUSTING - an opposing Butterfly passing through your Scale Dust. " +
                 "Paid into CombatPoints by CombatHitScoring.Credit once per pass per victim.")]
        [Min(0)] [SerializeField] int dustPoints = 1;

        [Tooltip("Points for any OTHER combat hit class. Zero on purpose: the Butterfly carries no " +
                 "gun and no blast that reports, and a rule that paid for one would hide a " +
                 "mis-authored vessel roster behind a working scoreboard.")]
        [Min(0)] [SerializeField] int otherHitPoints = 0;

        public int DustPoints => dustPoints;

        public override int PointsForCombatHit(CombatHitClass hitClass) =>
            hitClass == CombatHitClass.Strike ? dustPoints : otherHitPoints;

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
                    : $"{Remaining(gameData, s.Domain)} Points Left",
                $"{s.StrikeHitsLanded} dustings")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "DUSTUP TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "POINTS LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
