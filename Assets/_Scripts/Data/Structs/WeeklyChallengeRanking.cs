using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// One row of the weekly challenge leaderboard, already resolved into what a UI row needs.
    ///
    /// <para>Deliberately a project type rather than the UGS entry it is built from: the service
    /// owns every piece of Unity Gaming Services API surface, and a view that took a
    /// <c>Unity.Services.Leaderboards.Models.LeaderboardEntry</c> would drag the SDK into the UI
    /// layer and break the day the package renames a field. It also keeps the two OTHER
    /// <c>LeaderboardEntry</c> types already in this project (the PlayFab one and
    /// <see cref="LeaderboardEntry"/>) from becoming three names for one idea.</para>
    /// </summary>
    [Serializable]
    public struct WeeklyChallengeRanking
    {
        /// <summary>1-based, as UGS ranks it. 0 when the player has no entry.</summary>
        public int Rank;

        /// <summary>UGS player id — the one stable key. Names are not unique.</summary>
        public string PlayerId;

        /// <summary>Display name as UGS holds it, already stripped of its <c>#1234</c> suffix.</summary>
        public string PlayerName;

        /// <summary>
        /// SECONDS taken to complete the objective. Lower is better — the whole leaderboard is
        /// "who finished it first".
        /// </summary>
        public double Seconds;

        /// <summary>True for the signed-in player's own row, so a view can mark it.</summary>
        public bool IsLocalPlayer;

        /// <summary>
        /// Index into <c>SO_ProfileIconList</c>, or <see cref="NoAvatar"/> when this row carries
        /// none.
        ///
        /// <para><b>No avatar travels with a leaderboard entry on its own.</b> UGS holds a player
        /// id, a name, a rank and a score — not a profile. The id is carried here because the
        /// SUBMIT stamps it into the entry's metadata, which is the one field a score can take
        /// with it. So a row has a real face only if that player submitted after this shipped;
        /// every older entry resolves to <see cref="NoAvatar"/> and keeps the template's art, which
        /// is why a view must treat the fallback as normal rather than as a failure.</para>
        /// </summary>
        public int AvatarId;

        /// <summary>
        /// The challenge PERIOD this entry was submitted for — the UTC-Monday week key
        /// (<c>2026-09-07</c>), or a test period ("T42"). <c>null</c> when the row told us
        /// nothing, which is every entry submitted before the stamp shipped.
        ///
        /// <para><b>It exists because a leaderboard row does not otherwise say which week it
        /// belongs to.</b> The board is ONE board reset weekly by UGS, and that reset is a
        /// service-side schedule nothing in this code can enforce (the client SDK has no reset
        /// call) — so when it is missing or misaligned, last week's times stay on the board and
        /// rank above this week's forever
        /// (the score is a time and the sort is ascending, so an old fast run never falls off).
        /// The rows are real, the names are real, the times are real; they are simply answers to
        /// a DIFFERENT question than the one the panel is asking. Stamping the period is what
        /// lets a read tell the two apart — see <see cref="IsForPeriod"/>.</para>
        /// </summary>
        public string PeriodKey;

        /// <summary>"This row told us nothing about its avatar." Deliberately -1 rather than 0,
        /// because 0 is a REAL icon id and a missing avatar would silently show as that one.</summary>
        public const int NoAvatar = -1;

        /// <summary>True when <see cref="AvatarId"/> names an icon rather than the absence of one.</summary>
        public bool HasAvatar => AvatarId >= 0;

        /// <summary>The metadata field an avatar id travels in. One character on purpose: leaderboard
        /// metadata is size-capped and this is the whole payload.</summary>
        public const string AvatarMetadataKey = "a";

        /// <summary>The metadata field the challenge period travels in. One character for the same
        /// reason as the avatar's, and a DIFFERENT one so the two can be read independently — an
        /// entry may legitimately carry a period and no avatar.</summary>
        public const string PeriodMetadataKey = "w";

        /// <summary>
        /// The avatar id out of a leaderboard entry's metadata JSON, or <see cref="NoAvatar"/>.
        ///
        /// <para>Read with a small hand-rolled scan rather than a JSON parser because the payload
        /// is one integer under a one-character key and this runs once per row per fetch. It fails
        /// to <see cref="NoAvatar"/> on anything it does not recognise — an entry submitted before
        /// avatars were carried, a future field, a truncated string, a negative — which is the same
        /// state as an entry with no metadata at all and is drawn by the view as normal rather than
        /// as an error.</para>
        ///
        /// <para>Lives here rather than in the service because the STRUCT owns the field, so it
        /// owns how the field is recovered from a payload — and because a hand-rolled parser is
        /// exactly the kind of thing that fails silently and therefore has to be testable.</para>
        /// </summary>
        public static int ReadAvatarIdFromMetadata(string metadata)
        {
            int i = ValueStart(metadata, AvatarMetadataKey);
            if (i < 0) return NoAvatar;

            // A number submitted as a STRING ({"a":"7"}) is still an avatar id.
            if (i < metadata.Length && metadata[i] == '"') i++;

            int start = i;
            while (i < metadata.Length && char.IsDigit(metadata[i])) i++;

            return i > start && int.TryParse(metadata.Substring(start, i - start), out int id) && id >= 0
                ? id
                : NoAvatar;
        }

        /// <summary>
        /// The period key out of a leaderboard entry's metadata JSON, or <c>null</c>.
        ///
        /// <para>Null for every entry submitted before the stamp shipped, for a truncated payload,
        /// and for an empty string — all of which are the same state ("this row does not say which
        /// week it is from") and are treated as NOT the current period by
        /// <see cref="IsForPeriod"/>. That is the deliberate direction: an unstamped row on a board
        /// that should have reset is exactly the row we are trying not to show.</para>
        /// </summary>
        public static string ReadPeriodKeyFromMetadata(string metadata)
        {
            int i = ValueStart(metadata, PeriodMetadataKey);
            if (i < 0 || i >= metadata.Length) return null;

            if (metadata[i] == '"')
            {
                int close = metadata.IndexOf('"', i + 1);
                return close > i + 1 ? metadata.Substring(i + 1, close - i - 1) : null;
            }

            // Unquoted is not a shape this game writes, but a bare token up to the next delimiter
            // is still unambiguous, and reading it costs less than deciding it is corrupt.
            int end = i;
            while (end < metadata.Length &&
                   metadata[end] != ',' && metadata[end] != '}' && metadata[end] != ' ') end++;
            return end > i ? metadata.Substring(i, end - i) : null;
        }

        /// <summary>
        /// Index of a metadata field's VALUE — just past its colon and any spaces — or -1.
        ///
        /// <para>The key is matched <b>with its quotes</b> so a longer key that merely starts with
        /// it (<c>"ab"</c>) is never mistaken for it. Shared by both readers so a fix to the scan
        /// cannot land on one field and miss the other.</para>
        /// </summary>
        static int ValueStart(string metadata, string key)
        {
            if (string.IsNullOrEmpty(metadata)) return -1;

            string token = "\"" + key + "\"";
            int at = metadata.IndexOf(token, StringComparison.Ordinal);
            if (at < 0) return -1;

            int colon = metadata.IndexOf(':', at + token.Length);
            if (colon < 0) return -1;

            int i = colon + 1;
            while (i < metadata.Length && metadata[i] == ' ') i++;
            return i;
        }

        /// <summary>
        /// Whether a row belongs to the period being ranked.
        ///
        /// <para>A row that does not say (<c>null</c>, empty) is <b>not</b> this period. An unknown
        /// CURRENT period is the opposite — with nothing to judge against, every row passes, because
        /// emptying a board over a missing catalog would be a worse answer than showing it.</para>
        /// </summary>
        public static bool IsForPeriod(string rowPeriodKey, string currentPeriodKey) =>
            string.IsNullOrEmpty(currentPeriodKey) ||
            string.Equals(rowPeriodKey, currentPeriodKey, StringComparison.Ordinal);

        /// <summary>
        /// Drop every row that is not from <paramref name="currentPeriodKey"/> and return how many
        /// went. <b>Ranks are re-numbered 1..n only when something was dropped</b> — a board that
        /// reset correctly has nothing to drop, and its rows keep the true ranks UGS gave them.
        ///
        /// <para>When rows ARE dropped, renumbering is the same argument the Friends scope already
        /// makes: a list showing 1st, 4th, 812th is a board with most of its rows missing rather
        /// than a ranking of what is left.</para>
        /// </summary>
        public static int RetainPeriod(List<WeeklyChallengeRanking> rows, string currentPeriodKey)
        {
            if (rows == null || rows.Count == 0 || string.IsNullOrEmpty(currentPeriodKey)) return 0;

            int kept = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!IsForPeriod(rows[i].PeriodKey, currentPeriodKey)) continue;
                rows[kept++] = rows[i];
            }

            int dropped = rows.Count - kept;
            if (dropped == 0) return 0;

            rows.RemoveRange(kept, dropped);
            Renumber(rows);
            return dropped;
        }

        /// <summary>
        /// Re-rank a list 1..n in the order it is already in.
        ///
        /// <para>Shared by <see cref="RetainPeriod"/> and by the paged read, which filter in
        /// different places and must not end up with different ideas of what a rank means. Only
        /// ever called when rows were actually dropped — see <see cref="RetainPeriod"/> for why a
        /// clean board keeps the ranks UGS gave it. It also decides ties the same way either
        /// route: UGS may hand two players the same rank, so POSITION is the only basis that
        /// stays 1..n once anything is removed.</para>
        /// </summary>
        public static void Renumber(List<WeeklyChallengeRanking> rows)
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.Rank = i + 1;
                rows[i] = row;
            }
        }

        /// <summary>mm:ss.cc — the reading a race time wants.</summary>
        public string FormatTime() => FormatSeconds(Seconds);

        /// <summary>
        /// mm:ss.cc, clamped at zero.
        ///
        /// <para>Centiseconds rather than whole seconds because this is a race and ties would
        /// otherwise be routine: a 60-second challenge at whole-second resolution has 60 possible
        /// scores, so a full board would be mostly ties broken by who submitted first — which
        /// reads as an arbitrary order.</para>
        ///
        /// <para><b>Rounded to the nearest centisecond, never floored, and the whole value is
        /// converted at once.</b> The obvious implementation — take the whole seconds, then
        /// <c>floor((seconds - whole) * 100)</c> — prints <c>47.3</c> as <b>0:47.29</b>, because
        /// the double nearest 47.3 is a hair below it and the subtraction keeps the whole error.
        /// Converting to centiseconds in one step and rounding removes both the subtraction and
        /// the bias. The 0.005 s a round can add is a time displayed marginally SLOWER than the
        /// run, which is the safe direction for a leaderboard — a floor would print times the
        /// player did not achieve.</para>
        /// </summary>
        public static string FormatSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0d) seconds = 0d;
            if (double.IsInfinity(seconds)) return "--:--.--";

            long cs = (long)Math.Round(seconds * 100d, MidpointRounding.AwayFromZero);
            if (cs < 0L) cs = 0L;

            return $"{cs / 6000L}:{cs / 100L % 60L:D2}.{cs % 100L:D2}";
        }
    }
}
