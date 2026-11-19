using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Nucleus Rush ("Brood Rush"): points, not golf. Every fauna wave the cell births
    /// under a domain's nucleus claim scores that domain one brood (tracked on a
    /// representative player's <c>GoalsScored</c>, aggregated by domain like Astro
    /// League goals). The turn ends when an active domain's brood sum reaches
    /// <see cref="GameDataSO.GoalTargetCount"/> (default 3, authored via
    /// Tools &gt; Cosmic Shore &gt; End Game Conditions) - a race to N waves.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/BroodRush", fileName = "BroodRushScoringRule")]
    public class BroodRushScoringRuleSO : ScoringRuleSO
    {
        [Header("Volume books (Garrett, 2026-11-18)")]
        [Tooltip("Under each pilot's row: their own performance, independent of who won. {vc} the mass contribution " +
                 "(volume they increased in their own domain: laid, grown, stolen, shielded), {vd} the space " +
                 "contribution (volume they reduced from the opposing domains: destroyed or stolen), {total} the two " +
                 "added. Empty shows no books line.")]
        [SerializeField] string pilotBooksLine = "{vc} VC  {vd} VD  {total} total";
        [Tooltip("Added after a pilot's books when they broke any super-shielded prism of another team: {supers} the " +
                 "count. Empty shows nothing.")]
        [SerializeField] string superShieldsLine = "  {supers} super-shields broken";
        [Tooltip("Under each team's name: its pilots' books added, and {left} the volume the team still owned at the end.")]
        [SerializeField] string teamBooksLine = "{vc} VC  {vd} VD  {total} total  {left} left";
        [Tooltip("Added under a two-team match's team books when the difference of the team totals ({totals}) is not " +
                 "the difference of the volume left ({left}): volume that moved by something no pilot did (fauna, " +
                 "a pilot destroying their own team's, shrinking) or an event the books missed. Empty hides it.")]
        [SerializeField] string booksGapLine = "\nTotals differ by {totals}, volume left by {left}";

        protected override int TargetCount(GameDataSO gameData) => gameData.GoalTargetCount;

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            int target = TargetCount(gameData);
            if (target > 0)
            {
                int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
                for (int i = 0; i < dc; i++)
                {
                    var d = GameDataSO.ActiveDomains[i];
                    if (ScoringMetrics.SumByDomain(gameData, metric, d) >= target)
                    {
                        winner = d;
                        return true;
                    }
                }
            }
            winner = Domains.Blue;
            return false;
        }

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.GoalsScored;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var ordered = gameData.RoundStatsList.OrderByDescending(s => s.Score);

            var books = gameData.VolumeLedger;
            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
                s.Name,
                s.Domain,
                s.Score,
                $"{(int)s.Score} Brood{((int)s.Score == 1 ? "" : "s")}",
                books.TryGet(s.Name, out var line) ? PilotBooks(line) : null)).ToList();

            return ScoreResultBuilder.BuildRanked(rows);
        }

        /// <summary>
        /// The team's books: its pilots' lines added (Garrett: "those totals should be added for each team"), and what
        /// it owned at the end. In a two-team match the difference of the totals should be the difference of the
        /// volume left; when it is not, the gap line says by how much.
        /// </summary>
        public override string TeamLine(GameDataSO gameData, Domains domain)
        {
            if (gameData?.RoundStatsList == null || gameData.VolumeLedger.Count == 0 || string.IsNullOrEmpty(teamBooksLine))
                return null;
            var team = TeamBooks(gameData, domain);
            string text = TemplateText.Fill(teamBooksLine,
                "vc", FormatVolume(team.Created),
                "vd", FormatVolume(team.Reduced),
                "total", FormatVolume(team.Total),
                "left", FormatVolume(team.HasCount ? team.Counted : team.Remaining));

            var teams = gameData.RoundStatsList.Where(s => s != null).Select(s => s.Domain).Distinct().ToList();
            if (teams.Count == 2 && team.HasCount && !string.IsNullOrEmpty(booksGapLine))
            {
                var other = TeamBooks(gameData, teams[0] == domain ? teams[1] : teams[0]);
                float totals = team.Total - other.Total;
                float left = team.Counted - other.Counted;
                if (!VolumeLedger.Balances(totals, left))
                    text += TemplateText.Fill(booksGapLine, "totals", FormatVolume(totals), "left", FormatVolume(left));
            }
            return text;
        }

        static VolumeLedger.Line TeamBooks(GameDataSO gameData, Domains domain) =>
            gameData.VolumeLedger.Sum(gameData.RoundStatsList.Where(s => s != null && s.Domain == domain).Select(s => s.Name));

        string PilotBooks(VolumeLedger.Line line)
        {
            if (string.IsNullOrEmpty(pilotBooksLine)) return null;
            string text = TemplateText.Fill(pilotBooksLine,
                "vc", FormatVolume(line.Created),
                "vd", FormatVolume(line.Reduced),
                "total", FormatVolume(line.Total));
            if (line.SuperShieldsBroken > 0 && !string.IsNullOrEmpty(superShieldsLine))
                text += TemplateText.Fill(superShieldsLine, "supers", line.SuperShieldsBroken.ToString());
            return text;
        }

        /// <summary>Whole units under ten thousand, then thousands with one decimal ("12.3k").</summary>
        public static string FormatVolume(float v)
        {
            float a = Mathf.Abs(v);
            if (a < 10000f) return Mathf.RoundToInt(v).ToString();
            if (a < 10000000f) return (v / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
            return (v / 1000000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            int diff = DomainDelta(gameData);
            return new ScoreReveal(
                didWin ? "VICTORY" : "DEFEAT",
                $"{(didWin ? "WON" : "LOST")} BY {diff} BROOD{(diff == 1 ? "" : "S")}",
                (int)localStats.Score,
                false);
        }
    }
}
