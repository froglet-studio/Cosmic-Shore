using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Core
{
    /// <summary>
    /// What the friends panel's Add Friend field means to the UGS Friends service.
    ///
    /// <para>UGS resolves a NAME only in its full "Name#1234" form: the Friends SDK sends the typed
    /// text verbatim as the target's profile name (<c>WrappedFriendsApi.AddFriendByNameAsync</c>,
    /// <c>MemberIdentity(profileName: name)</c>), and the player-names service stores every name with
    /// its "#1234" discriminator. The game strips that discriminator from every name it shows
    /// (friend rows, scoreboards, invites), so a player who types the name they can see never
    /// reaches anyone by name. Two routes therefore exist:</para>
    /// <list type="bullet">
    /// <item>Text WITH a discriminator is a full name and goes to UGS by name, as typed (whitespace
    /// removed: a UGS name never contains any).</item>
    /// <item>Text WITHOUT one is matched against the pilots this machine already knows (the online
    /// lobby, the party, recently played with, the friend lists) and, when exactly one other pilot
    /// carries that name, the request goes to that pilot's player id. Names compare by letters and
    /// digits only, ignoring case: the same compaction <c>PlayerDataService</c> applies when it
    /// mirrors a display name into UGS, so "Sky Walker", "SkyWalker" and "skywalker" are one pilot.</item>
    /// </list>
    /// <para>Anything else is refused locally with a reason, so the player is told to type the full
    /// Name#1234 instead of watching a request fail on the server. Pure: no Unity or UGS types, so
    /// the edit-mode tests cover it without a scene or a sign-in.</para>
    /// </summary>
    public static class FriendNameResolver
    {
        /// <summary>What a typed name resolved to.</summary>
        public enum Outcome
        {
            /// <summary>Nothing typed.</summary>
            Empty = 0,
            /// <summary>A full "Name#1234": send by name (<see cref="Resolution.FullName"/>).</summary>
            FullName = 1,
            /// <summary>Exactly one known pilot carries the name: send by id (<see cref="Resolution.PlayerId"/>).</summary>
            KnownPilot = 2,
            /// <summary>Several known pilots carry the name: only their full Name#1234 can tell them apart.</summary>
            Ambiguous = 3,
            /// <summary>No known pilot carries the name: only their full Name#1234 can reach them.</summary>
            Unknown = 4,
            /// <summary>The name is the local player's own.</summary>
            Self = 5,
        }

        /// <summary>A pilot this machine knows by id and shown name.</summary>
        public readonly struct KnownPilot
        {
            public readonly string PlayerId;
            public readonly string DisplayName;

            public KnownPilot(string playerId, string displayName)
            {
                PlayerId = playerId;
                DisplayName = displayName;
            }
        }

        /// <summary>The outcome, and what the send needs for it.</summary>
        public readonly struct Resolution
        {
            public readonly Outcome Outcome;
            /// <summary>The full "Name#1234" to send by name; set for <see cref="Outcome.FullName"/>.</summary>
            public readonly string FullName;
            /// <summary>The player id to send to; set for <see cref="Outcome.KnownPilot"/>.</summary>
            public readonly string PlayerId;
            /// <summary>The name to show in feedback: the matched pilot's, the full name, or what was typed.</summary>
            public readonly string DisplayName;

            public Resolution(Outcome outcome, string fullName, string playerId, string displayName)
            {
                Outcome = outcome;
                FullName = fullName;
                PlayerId = playerId;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// Resolves <paramref name="typed"/> against <paramref name="known"/>.
        /// </summary>
        /// <param name="typed">The Add Friend field's text.</param>
        /// <param name="known">Every pilot the caller knows; duplicates of one id are fine.</param>
        /// <param name="localPlayerId">The local player's id (never a match), or null.</param>
        /// <param name="localFullName">The local player's own "Name#1234", or null when unknown.</param>
        public static Resolution Resolve(string typed, IEnumerable<KnownPilot> known, string localPlayerId,
                                         string localFullName)
        {
            string text = typed?.Trim();
            if (string.IsNullOrEmpty(text))
                return new Resolution(Outcome.Empty, null, null, string.Empty);

            if (IsFullName(text))
            {
                string fullName = RemoveWhitespace(text);
                if (!string.IsNullOrEmpty(localFullName) &&
                    string.Equals(fullName, RemoveWhitespace(localFullName), StringComparison.Ordinal))
                    return new Resolution(Outcome.Self, null, null, fullName);

                return new Resolution(Outcome.FullName, fullName, null, fullName);
            }

            string key = Normalize(text);
            if (key.Length == 0)
                return new Resolution(Outcome.Unknown, null, null, text);

            string matchId = null;
            string matchName = null;
            bool ambiguous = false;
            bool matchedSelf = false;

            if (known != null)
            {
                foreach (var pilot in known)
                {
                    if (string.IsNullOrEmpty(pilot.PlayerId)) continue;
                    if (Normalize(pilot.DisplayName) != key) continue;

                    if (!string.IsNullOrEmpty(localPlayerId) && pilot.PlayerId == localPlayerId)
                    {
                        matchedSelf = true;
                        continue;
                    }

                    if (matchId == null)
                    {
                        matchId = pilot.PlayerId;
                        matchName = pilot.DisplayName;
                    }
                    else if (matchId != pilot.PlayerId)
                    {
                        ambiguous = true;
                    }
                }
            }

            if (ambiguous)
                return new Resolution(Outcome.Ambiguous, null, null, text);
            if (matchId != null)
                return new Resolution(Outcome.KnownPilot, null, matchId, matchName);
            if (matchedSelf || (!string.IsNullOrEmpty(localFullName) && Normalize(StripDiscriminator(localFullName)) == key))
                return new Resolution(Outcome.Self, null, null, text);

            return new Resolution(Outcome.Unknown, null, null, text);
        }

        /// <summary>
        /// True for text shaped like a full UGS name: something before a '#' and something after it.
        /// </summary>
        public static bool IsFullName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int hash = text.LastIndexOf('#');
            return hash > 0 && hash < text.Length - 1;
        }

        /// <summary>A name reduced to its letters and digits, lower case: the key names are compared by.</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        static string StripDiscriminator(string fullName)
        {
            int hash = fullName.LastIndexOf('#');
            return hash > 0 ? fullName.Substring(0, hash) : fullName;
        }

        static string RemoveWhitespace(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (!char.IsWhiteSpace(c))
                    sb.Append(c);
            return sb.ToString();
        }
    }
}
