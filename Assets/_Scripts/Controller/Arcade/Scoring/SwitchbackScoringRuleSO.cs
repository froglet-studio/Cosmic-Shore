using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Switchback: a golf-timed race through an ORDERED course of switch gates. The first domain
    /// whose LEAD RUNNER threads the last gate wins; that domain's pilots all score the finish
    /// time, everyone else a sentinel encoding how much course their best pilot had left.
    ///
    /// <para><b>The one thing this rule does differently from every other race rule is the
    /// fold.</b> Every pilot flies the SAME course, so a domain's progress is its best pilot's,
    /// not the sum of its pilots' - <see cref="DomainValue"/> overrides to
    /// <see cref="ScoringMetrics.BestByDomain"/>. Under the default sum a two-pilot domain would
    /// read as twice the course, cross the target at half the gates, and beat a one-pilot domain
    /// that had actually flown further. Because the override is on the ONE seam every domain
    /// reader goes through, the end condition, the "remaining" readout, the placement order, the
    /// winner resolution and the HUD's own domain boxes all move together.</para>
    ///
    /// <para>Consequently a teammate never adds to the score - which is the honest reading of a
    /// race, and leaves the team play where it belongs: running interference, and the Dolphin's
    /// blast cone, which debuffs a rival pilot in every mode.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Switchback", fileName = "SwitchbackScoringRule")]
    public class SwitchbackScoringRuleSO : ScoringRuleSO
    {
        /// <summary>
        /// A domain's course progress is its LEAD RUNNER's gate count. See the class summary -
        /// this is the whole reason the fold is a seam rather than four copies of a sum.
        /// </summary>
        public override int DomainValue(GameDataSO gameData, Domains domain) =>
            ScoringMetrics.BestByDomain(gameData, metric, domain);

        protected override int TargetCount(GameDataSO gameData) => gameData.SwitchTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = Domains.Blue;

            // Never end on a target of 0. The monitor publishes it from the server and it
            // reaches a client one NetworkVariable tick later, so an unguarded ">= 0" would
            // declare a winner on the first poll of every match.
            int target = TargetCount(gameData);
            if (target <= 0) return false;

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
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.Domain == winner
                    ? finishTime
                    : GolfScoreSentinels.EncodeSkimRaceLoserScore(Remaining(gameData, stats.Domain));
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList
                .OrderBy(s => s.Score)
                .ThenByDescending(s => s.SwitchesThreaded)
                .ThenBy(s => s.Name, StringComparer.Ordinal);

            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                GolfScoreSentinels.IsFinishTime(s.Score)
                    ? ScoreResultBuilder.FormatTime(s.Score)
                    : $"{Remaining(gameData, s.Domain)} Gates Left",
                // The per-player line is that PILOT's own run, not their domain's - the domain
                // fold is the lead runner, so without this a trailing teammate's row would
                // silently show the ace's gates.
                $"{LiveMetric(s)} Gates")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "COURSE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", "GATES LEFT", Remaining(gameData, localStats.Domain), false);
    }
}
