using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Data;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Single home for turning <see cref="TournamentDataSO"/> standings into display strings, so the
    /// between-game loading splash (<c>BootStatusBroadcaster</c>) and the end-of-shuffle results screen
    /// (<c>TournamentSceneView</c>) format identically and can't drift apart (DRY). Pure functions - no
    /// Unity object access - so they are trivially unit-testable.
    ///
    /// Scoring is per-DOMAIN (a row per team, not per player), so callers pass the local player's domain
    /// (<paramref name="localDomain"/>); the row for that domain is tagged "(You)" so each peer can spot
    /// its own team's line. Pass <see cref="Domains.Blue"/> (the "no team" sentinel - never a standings
    /// row) to tag nothing.
    /// </summary>
    public static class TournamentStandingsFormatter
    {
        /// <summary>
        /// Compact running standings for the between-game loading splash: a "{MODE} - first to {target}"
        /// header then each domain "{Domain}  {points}", best-first. The local player's domain is tagged
        /// "(You)".
        /// </summary>
        public static string FormatRunning(TournamentDataSO data, Domains localDomain = Domains.Blue)
        {
            if (data == null) return string.Empty;

            var standings = data.BuildSortedStandings();
            var sb = new StringBuilder();
            sb.AppendLine($"{data.ModeName.ToUpperInvariant()} - first to {data.EffectiveWinTarget}");

            // What's loading next (the host's random draw stamps these): mode name + its rolled intensity.
            if (!string.IsNullOrEmpty(data.NextGameName))
                sb.AppendLine($"Up next: {data.NextGameName} · Intensity {data.NextGameIntensity}");

            for (int i = 0; i < standings.Count; i++)
                sb.AppendLine($"{standings[i].Domain}{YouTag(standings[i].Domain, localDomain)}  {standings[i].TotalPoints}");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Lightweight between-round reveal for the loading splash: just the leading domain plus the
        /// "up next" mode + intensity (host-only - clients learn the mode from the synced game config
        /// once the scene loads). The DETAILED standings now live in the Maelstrom hub scene, so the
        /// splash stays light. The local player's domain is tagged "(You)".
        /// </summary>
        public static string FormatConnecting(TournamentDataSO data, Domains localDomain = Domains.Blue)
        {
            if (data == null) return string.Empty;

            var sb = new StringBuilder();

            var standings = data.BuildSortedStandings();
            if (standings.Count > 0)
                sb.AppendLine($"{standings[0].Domain.ToString().ToUpperInvariant()}{YouTag(standings[0].Domain, localDomain)} leading");

            if (!string.IsNullOrEmpty(data.NextGameName))
                sb.Append($"{data.NextGameName} · Intensity {data.NextGameIntensity}");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Full end-of-shuffle results: final per-domain standings (the local player's domain tagged
        /// "(You)"), then one block per game actually played listing the domains by their placement that
        /// game (the lineup is random + variable length and the played-mode sequence isn't tracked, so
        /// games are labelled generically).
        /// </summary>
        public static string FormatFinal(TournamentDataSO data, Domains localDomain = Domains.Blue)
        {
            if (data == null) return string.Empty;

            var standings = data.BuildSortedStandings();
            var sb = new StringBuilder();

            sb.AppendLine("<b>FINAL STANDINGS</b>");
            for (int i = 0; i < standings.Count; i++)
                sb.AppendLine($"{i + 1}. {standings[i].Domain}{YouTag(standings[i].Domain, localDomain)} - {standings[i].TotalPoints} pts");

            for (int g = 0; g < data.GamesPlayed; g++)
            {
                sb.AppendLine();
                sb.AppendLine($"<b>Game {g + 1}</b>");
                foreach (var s in standings.Where(s => g < s.Placements.Count).OrderBy(s => s.Placements[g]))
                    sb.AppendLine($"  {Ordinal(s.Placements[g])}: {s.Domain}");
            }

            return sb.ToString().TrimEnd();
        }

        // "(You)" marker appended beside the local player's domain row (bolded so the owner spots it at a
        // glance). Empty for every other domain - and for Domains.Blue, which never appears in standings.
        static string YouTag(Domains domain, Domains localDomain) =>
            domain == localDomain ? " <b>(You)</b>" : string.Empty;

        static string Ordinal(int n) => n switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{n}th",
        };
    }
}
