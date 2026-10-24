using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rev 8 scoreboard's layout (Docs/MENU_REDESIGN_B.md §49, option B "glass ladder"): the ranked results grouped
    /// under team headers when any domain fields two or more pilots, the team's score said once in its header when every
    /// member shares it (a team race time), and each pilot's share of the team's count. Pure, so it is tested without a
    /// scene.
    /// </summary>
    public static class ScoreboardLadder
    {
        public readonly struct Entry
        {
            /// <summary>A team header; otherwise a pilot row.</summary>
            public readonly bool IsHeader;
            /// <summary>The row's index into the results (-1 for a header).</summary>
            public readonly int ResultIndex;
            public readonly Domains Domain;
            /// <summary>The header's team won.</summary>
            public readonly bool Won;
            /// <summary>Header: the score every member shares, or null. Row: unused.</summary>
            public readonly string SharedScore;
            /// <summary>Row: its own score is shown (no header carries it).</summary>
            public readonly bool ShowsScore;
            /// <summary>Row: its share of the team's count, 0-1, or -1 when there is none to draw.</summary>
            public readonly float Share;

            Entry(bool header, int index, Domains domain, bool won, string shared, bool showsScore, float share)
            {
                IsHeader = header;
                ResultIndex = index;
                Domain = domain;
                Won = won;
                SharedScore = shared;
                ShowsScore = showsScore;
                Share = share;
            }

            public static Entry Header(Domains domain, bool won, string shared) => new(true, -1, domain, won, shared, false, -1f);
            public static Entry Row(int index, Domains domain, bool won, bool showsScore, float share) =>
                new(false, index, domain, won, null, showsScore, share);
        }

        /// <summary>Lays out <paramref name="results"/> (ranked, best first). <paramref name="winner"/> is the winning
        /// domain; Blue (no domain) means the first result's domain won.</summary>
        public static List<Entry> Build(IReadOnlyList<ScoreResult> results, Domains winner)
        {
            var entries = new List<Entry>();
            if (results == null || results.Count == 0) return entries;
            if (winner == Domains.Blue) winner = results[0].Domain;

            var order = new List<Domains>();
            var members = new Dictionary<Domains, List<int>>();
            for (int i = 0; i < results.Count; i++)
            {
                var d = results[i].Domain;
                if (!members.TryGetValue(d, out var list)) { members[d] = list = new List<int>(); order.Add(d); }
                list.Add(i);
            }

            bool grouped = false;
            foreach (var list in members.Values) grouped |= list.Count > 1;

            if (!grouped)
            {
                for (int i = 0; i < results.Count; i++)
                    entries.Add(Entry.Row(i, results[i].Domain, results[i].Domain == winner, true, -1f));
                return entries;
            }

            foreach (var domain in order)
            {
                var list = members[domain];
                string shared = SharedScore(results, list);
                bool won = domain == winner;
                entries.Add(Entry.Header(domain, won, shared));
                var shares = Shares(results, list);
                for (int k = 0; k < list.Count; k++)
                    entries.Add(Entry.Row(list[k], domain, won, shared == null, shares[k]));
            }
            return entries;
        }

        /// <summary>The score text every listed result shares, or null when they differ or there is one result.</summary>
        public static string SharedScore(IReadOnlyList<ScoreResult> results, List<int> indices)
        {
            if (indices.Count < 2) return null;
            string first = results[indices[0]].ScoreText;
            if (string.IsNullOrEmpty(first)) return null;
            for (int k = 1; k < indices.Count; k++)
                if (results[indices[k]].ScoreText != first) return null;
            return first;
        }

        /// <summary>Each listed result's share of the team's total, read from the leading number of its secondary line
        /// ("12 Crystals"); all -1 when any line has no number or the total is zero.</summary>
        public static float[] Shares(IReadOnlyList<ScoreResult> results, List<int> indices)
        {
            var shares = new float[indices.Count];
            var counts = new int[indices.Count];
            int total = 0;
            bool ok = true;
            for (int k = 0; k < indices.Count; k++)
            {
                ok &= TryLeadingNumber(results[indices[k]].Secondary, out counts[k]);
                total += counts[k];
            }
            for (int k = 0; k < shares.Length; k++)
                shares[k] = ok && total > 0 ? (float)counts[k] / total : -1f;
            return shares;
        }

        /// <summary>The whole number a line starts with ("12 Crystals" -> 12).</summary>
        public static bool TryLeadingNumber(string line, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(line)) return false;
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            int start = i;
            while (i < line.Length && line[i] >= '0' && line[i] <= '9' && i - start < 9)
            {
                value = value * 10 + (line[i] - '0');
                i++;
            }
            return i > start;
        }
    }
}
