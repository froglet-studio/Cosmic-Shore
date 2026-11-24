using System.Collections.Generic;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gives every pilot of a match a name no other pilot has. The roster, the scoreboard, the score sync and the
    /// end conditions all key a pilot by <c>Name</c>, so two players who picked the same display name were one row:
    /// one of them was never added and each one's stats evicted the other's (seen 2026-11-16 with two accounts
    /// both called "ys1": three stats rows for four pilots, and the two machines disagreed on the shared row).
    ///
    /// <para>The rule is the same on every machine and needs nothing new on the wire: among the spawned players
    /// with the same display name, the one with the lowest NetworkObjectId keeps the name as it is, and each later
    /// one gets its place in that order (2, 3, ...) written into an authored template.</para>
    /// </summary>
    public static class RosterNames
    {
        /// <summary>
        /// How many of <paramref name="others"/> carry <paramref name="displayName"/> and sort before
        /// <paramref name="networkObjectId"/>. 0 means the name is this pilot's to keep unchanged.
        /// </summary>
        public static int NamesakesBefore(ulong networkObjectId, string displayName,
            IReadOnlyList<(ulong networkObjectId, string displayName)> others)
        {
            if (string.IsNullOrEmpty(displayName) || others == null) return 0;

            int before = 0;
            for (int i = 0, n = others.Count; i < n; i++)
            {
                var other = others[i];
                if (other.networkObjectId < networkObjectId && other.displayName == displayName)
                    before++;
            }
            return before;
        }

        /// <summary>
        /// The roster name for a pilot with <paramref name="namesakesBefore"/> same-named pilots ahead of it.
        /// <paramref name="template"/> is the authored line (tokens {name} and {number}). A template that is
        /// empty, or that leaves the name unchanged, cannot be allowed to bring the collision back, so the
        /// number alone is appended: digits, no words.
        /// </summary>
        public static string Compose(string displayName, int namesakesBefore, string template)
        {
            if (namesakesBefore <= 0 || string.IsNullOrEmpty(displayName)) return displayName;

            string number = (namesakesBefore + 1).ToString();
            string filled = TemplateText.Fill(template, "name", displayName, "number", number);
            bool tellsThemApart = !string.IsNullOrEmpty(filled) && filled != displayName
                                  && template.Contains("{number}");
            return tellsThemApart ? filled : displayName + " " + number;
        }
    }
}
