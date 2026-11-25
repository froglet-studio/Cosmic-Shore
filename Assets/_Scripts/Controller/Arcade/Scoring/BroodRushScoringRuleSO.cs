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
    /// under a domain's nucleus claim scores that domain one brood. The race is the
    /// domain's brood sum (the <c>Goals</c> metric; the controller keeps a team's broods on
    /// one representative's <c>GoalsScored</c> only so the domain sum carries them); the
    /// turn ends when an active domain's sum reaches <see cref="GameDataSO.GoalTargetCount"/>
    /// (default 3, End Game Conditions) - a race to N waves.
    ///
    /// <para>The end scoreboard (Garrett, 2026-11-18): a pilot's score is their volume books,
    /// VC (mass contribution) + VD (space contribution) from <see cref="VolumeLedger"/>, and a
    /// team's score is its pilots' added. Broods are the team's, said once on the team line,
    /// never stamped onto one pilot - the old board read "0 Broods" for every teammate but the
    /// first in the roster, which looked wildly lopsided on a balanced field.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/BroodRush", fileName = "BroodRushScoringRule")]
    public class BroodRushScoringRuleSO : ScoringRuleSO
    {
        [Header("Reveal")]
        [Tooltip("The reveal's header when the local pilot's team won.")]
        [SerializeField] string victoryHeader = "VICTORY";
        [Tooltip("The reveal's header when the local pilot's team lost.")]
        [SerializeField] string defeatHeader = "DEFEAT";
        [Tooltip("The reveal's label when the local pilot's team won: {diff} the brood margin. The value beside it " +
                 "is the pilot's own score (VC + VD).")]
        [SerializeField] string revealWonLine = "WON BY {diff} {broods}";
        [Tooltip("The reveal's label when the local pilot's team lost: {diff} the brood margin.")]
        [SerializeField] string revealLostLine = "LOST BY {diff} {broods}";
        [Tooltip("The word for one brood, filled into {broods} in the reveal lines.")]
        [SerializeField] string broodWordOne = "BROOD";
        [Tooltip("The word for several (or no) broods, filled into {broods} in the reveal lines.")]
        [SerializeField] string broodWordMany = "BROODS";

        [Header("Scoreboard (Garrett, 2026-11-18)")]
        [Tooltip("A pilot's score on their row: {score} their VC + VD as a whole number. Keep it a bare number - " +
                 "the team-versus board adds the rows up into the team's score. Empty shows no score.")]
        [SerializeField] string pilotScoreText = "{score}";
        [Tooltip("The team's broods, said once on its team line: {broods} the count, when it is not 1.")]
        [SerializeField] string teamBroodsLine = "{broods} Broods";
        [Tooltip("The team's broods when it holds exactly one ({broods} is 1).")]
        [SerializeField] string teamOneBroodLine = "1 Brood";

        [Header("Volume books (Garrett, 2026-11-18)")]
        [Tooltip("Under each pilot's row: how their score is made. {total} their score (VC + VD, a whole number - " +
                 "keep it first: the share bar reads the line's leading number), {vc} the mass contribution " +
                 "(volume they increased in their own domain: laid, grown, stolen, shielded), {vd} the space " +
                 "contribution (volume they reduced from the opposing domains: destroyed or stolen). Empty shows no " +
                 "books line.")]
        [SerializeField] string pilotBooksLine = "{total} = {vc} VC + {vd} VD";
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

        /// <summary>A pilot's score is their volume books' VC + VD (the server's ledger), never the team's broods.</summary>
        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                if (stats != null) stats.Score = PilotScore(gameData, stats.Name);
        }

        /// <summary>One pilot's score: VC + VD from the books, 0 for a pilot the books never saw.</summary>
        public static float PilotScore(GameDataSO gameData, string name) => gameData.VolumeLedger.Get(name).Total;

        /// <summary>A team's broods: the domain sum the race runs on.</summary>
        public int TeamBroods(GameDataSO gameData, Domains domain) => ScoringMetrics.SumByDomain(gameData, metric, domain);

        /// <summary>
        /// Rows ranked by VC + VD (the rank on each card). They are laid out team by team - the winning team first,
        /// then by broods - so the board's team grouping and the race agree; inside a team the best score leads.
        /// </summary>
        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var stats = gameData.RoundStatsList.Where(s => s != null).ToList();
            var rank = stats.OrderByDescending(s => s.Score).Select((s, i) => (s, i))
                            .ToDictionary(p => p.s, p => p.i + 1);
            var winner = gameData.WinnerDomain;
            var laidOut = stats
                .OrderBy(s => winner != Domains.Blue && s.Domain == winner ? 0 : 1)
                .ThenByDescending(s => TeamBroods(gameData, s.Domain))
                .ThenBy(s => (int)s.Domain)
                .ThenByDescending(s => s.Score);

            var books = gameData.VolumeLedger;
            var results = new List<ScoreResult>(stats.Count);
            foreach (var s in laidOut)
            {
                string scoreText = TemplateText.Fill(pilotScoreText, "score", Mathf.RoundToInt(s.Score).ToString());
                string secondary = books.TryGet(s.Name, out var line) ? PilotBooks(line) : null;
                results.Add(new ScoreResult(rank[s], s.Name, s.Domain, s.Score, scoreText, secondary));
            }
            return results;
        }

        /// <summary>
        /// The team's books: its pilots' lines added (Garrett: "those totals should be added for each team"), and what
        /// it owned at the end. In a two-team match the difference of the totals should be the difference of the
        /// volume left; when it is not, the gap line says by how much.
        /// </summary>
        public override string TeamLine(GameDataSO gameData, Domains domain)
        {
            if (gameData?.RoundStatsList == null) return null;
            int broods = TeamBroods(gameData, domain);
            string broodLine = broods == 1
                ? TemplateText.Fill(teamOneBroodLine, "broods", "1")
                : TemplateText.Fill(teamBroodsLine, "broods", broods.ToString());
            string books = TeamBooksText(gameData, domain);
            if (string.IsNullOrEmpty(broodLine)) return string.IsNullOrEmpty(books) ? null : books;
            return string.IsNullOrEmpty(books) ? broodLine : broodLine + "\n" + books;
        }

        string TeamBooksText(GameDataSO gameData, Domains domain)
        {
            if (gameData.VolumeLedger.Count == 0 || string.IsNullOrEmpty(teamBooksLine)) return null;
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
                "total", Mathf.RoundToInt(line.Total).ToString());
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
            string label = TemplateText.Fill(didWin ? revealWonLine : revealLostLine,
                "diff", diff.ToString(),
                "broods", diff == 1 ? broodWordOne : broodWordMany);
            return new ScoreReveal(didWin ? victoryHeader : defeatHeader, label,
                Mathf.RoundToInt(localStats.Score), false);
        }
    }
}
