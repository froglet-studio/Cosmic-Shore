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
    /// <para><b>One per 3.2 PETALS, not per pass</b> (since 2026-10-31). The dust shreds one petal of one
    /// random element on a Charge-scaled clock for as long as a rival stays inside it (1 s at rest, 0.1 s
    /// at Charge 10), and its reporter scores one Strike per 3.2 petals, a Strike's fleet price, so the
    /// score stays proportional to the petals taken. Hovering over a rival now pays; it used to score
    /// the pass that caught them and nothing after it.</para>
    ///
    /// Golf-timed like Undertow: the winning domain's pilots carry their finish time, losers a
    /// sentinel encoding their domain's deficit.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Dustup", fileName = "DustupScoringRule")]
    public class DustupScoringRuleSO : ScoringRuleSO
    {
        [Header("Point values")]
        [Tooltip("Points for one DUSTING - an opposing Butterfly passing through your Scale Dust. " +
                 "Paid into CombatPoints by CombatHitScoring.Credit once per 3.2 petals the dust shreds off a " +
                 "victim (one petal of one random element, every 1 s at Charge 0 to 0.1 s at Charge 10).")]
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
