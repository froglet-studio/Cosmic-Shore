using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Single home for turning <see cref="TournamentDataSO"/> standings into display strings, so the
    /// between-game loading splash (<c>BootStatusBroadcaster</c>) and the end-of-shuffle results screen
    /// (<c>TournamentSceneView</c>) format identically and can't drift apart (DRY). Pure functions — no
    /// Unity object access — so they are trivially unit-testable.
    /// </summary>
    public static class TournamentStandingsFormatter
    {
        /// <summary>
        /// Compact running standings for the between-game loading splash: a "{MODE} — first to {target}"
        /// header then each domain "{Domain}  {points}", best-first.
        /// </summary>
        public static string FormatRunning(TournamentDataSO data)
        {
            if (data == null) return string.Empty;

            var standings = data.BuildSortedStandings();
            var sb = new StringBuilder();
            sb.AppendLine($"{data.ModeName.ToUpperInvariant()} — first to {data.WinTarget}");

            // What's loading next (the host's random draw stamps these): mode name + its rolled intensity.
            if (!string.IsNullOrEmpty(data.NextGameName))
                sb.AppendLine($"Up next: {data.NextGameName} · Intensity {data.NextGameIntensity}");

            for (int i = 0; i < standings.Count; i++)
                sb.AppendLine($"{standings[i].Domain}  {standings[i].TotalPoints}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Full end-of-shuffle results: final per-domain standings, then one block per game actually
        /// played listing the domains by their placement that game (the lineup is random + variable
        /// length and the played-mode sequence isn't tracked, so games are labelled generically).
        /// </summary>
        public static string FormatFinal(TournamentDataSO data)
        {
            if (data == null) return string.Empty;

            var standings = data.BuildSortedStandings();
            var sb = new StringBuilder();

            sb.AppendLine("<b>FINAL STANDINGS</b>");
            for (int i = 0; i < standings.Count; i++)
                sb.AppendLine($"{i + 1}. {standings[i].Domain} — {standings[i].TotalPoints} pts");

            for (int g = 0; g < data.GamesPlayed; g++)
            {
                sb.AppendLine();
                sb.AppendLine($"<b>Game {g + 1}</b>");
                foreach (var s in standings.Where(s => g < s.Placements.Count).OrderBy(s => s.Placements[g]))
                    sb.AppendLine($"  {Ordinal(s.Placements[g])}: {s.Domain}");
            }

            return sb.ToString().TrimEnd();
        }

        static string Ordinal(int n) => n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th",
        };
    }
}
