using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A golf-timed race through an ORDERED course of switches - Switchback's gates and
    /// Breakwater's stations are the same fact, so both modes author an asset on this ONE rule.
    /// Switchback: The first domain
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
        [Tooltip("What one unit of this course is CALLED on the scoreboard and the defeat reveal - " +
                 "\"Gate\" for Switchback, \"Station\" for Breakwater. The rule is shared by both " +
                 "ordered-switch races, so the noun has to be authored rather than hardcoded; the " +
                 "goal row is unaffected because it is keyed on the METRIC and reads \"Thread " +
                 "switches\" for either.")]
        [SerializeField] string unitNoun = "Gate";

        /// <summary>
        /// The authored noun, or "Gate" when the asset predates this field.
        ///
        /// <para><b>The fallback is in CODE, deliberately, and not a field initializer.</b> Unity
        /// fills a key a serialized asset does not carry with the TYPE default - an EMPTY string -
        /// not with the initializer above, so the shipped SwitchbackScoringRule.asset (authored
        /// before this field existed) would otherwise render "3 Left" and " LEFT". That is the
        /// SpawnProfileSO trap the ecology notes record, reached from the scoring side: a new
        /// serialized field is a silent regression on every asset already on disk unless the
        /// reader treats "absent" as a real case.</para>
        /// </summary>
        string Unit => string.IsNullOrWhiteSpace(unitNoun) ? "Gate" : unitNoun;

        /// <summary>
        /// A domain's course progress is its LEAD RUNNER's gate count. See the class summary -
        /// this is the whole reason the fold is a seam rather than four copies of a sum.
        /// </summary>
        public override int DomainValue(GameDataSO gameData, Domains domain) =>
            ScoringMetrics.BestByDomain(gameData, metric, domain);

        protected override int TargetCount(GameDataSO gameData) => gameData.SwitchTargetCount;

        /// <summary>
        /// A PILOT's own remaining gates. The domain fold is the lead runner, so the base
        /// implementation's domain reading would tell a trailing teammate they had the ace's
        /// gates left - their goal row would say "12/20" while their objective arrow pointed at
        /// gate 4, and their scoreboard row would not add up. Everything that asks about the
        /// RACE (the end condition, the loser sentinel, the placement order) still reads the
        /// domain through <see cref="Remaining"/>.
        /// </summary>
        public override int RemainingForPlayer(GameDataSO gameData, IRoundStats stats) =>
            stats == null ? 0 : Mathf.Max(0, TargetCount(gameData) - LiveMetric(stats));

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
                    // Per PILOT, so the row adds up: gates flown + gates left = the course.
                    // The domain fold is the lead runner, so a domain reading here would sit a
                    // trailing teammate's own gate count beside the ace's remainder.
                    : $"{RemainingForPlayer(gameData, s)} {Unit}s Left",
                $"{LiveMetric(s)} {Unit}s")).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin) =>
            didWin
                ? new ScoreReveal("VICTORY", "COURSE TIME", (int)localStats.Score, true)
                : new ScoreReveal("DEFEAT", $"{Unit.ToUpperInvariant()}S LEFT",
                                  RemainingForPlayer(gameData, localStats), false);
    }
}
