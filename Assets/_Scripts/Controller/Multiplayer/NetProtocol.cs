// ─────────────────────────────────────────────────────────────────────────────
// NetProtocol.cs
// The running build's Netcode protocol version in every UGS session and lobby this game makes,
// and the filters that keep two builds that cannot play together from ever meeting.
//
// WHY (Docs/LAUNCH/READINESS.md §10 blocker 6; Docs/PartySystem/BUGS.md B35):
//   NetworkConfig.ProtocolVersion (Assets/_Prefabs/CORE/NetworkManager.prefab) is bumped with every
//   wire change, and Netcode refuses a mismatched joiner at the connection request. But the session
//   queries filtered only on game mode and seat count, so an old client and a new one found each
//   other, joined the same lobby, and the join died at the host with "NetworkConfig mismatch" - seen
//   by the player as a generic bounce.
//
// WHAT:
//   - One source of truth: the version is READ from the live NetworkManager's NetworkConfig, never
//     typed here (LocalTag).
//   - Every session this game creates carries it as an indexed session property (key "protocol",
//     String3): the matchmaking session, the presence lobby and the party session.
//   - The two QUERIED kinds (matchmaking, presence) also put it inside their String1 game-mode value
//     (Scope: "PRESENCE_LOBBY@p24", "SkimRace@p24"). That is what makes the gate hold against OLD
//     builds too: an old build filters on the bare game mode and never sees a new build's sessions,
//     and a new build filters on the scoped value and the protocol and never sees an old build's.
//     Each protocol therefore has its own presence lobby set, named deterministically by protocol,
//     and the presence converge (smallest id wins) runs inside that set - it cannot thrash between
//     two versions' lobbies because it never sees the other version's.
//   - The party session is private (joined by id from a presence row or an invite, both of which
//     already live in the protocol-scoped presence lobby). Its members also publish "protocol" as a
//     player property, and a join checks the members it finds the moment the join returns, before
//     the Netcode connection is awaited (PartySessionService.JoinByIdAsync): a member that publishes
//     a different version, or none (an old build), throws ProtocolMismatchException, which
//     PartyInviteController turns into its authored "update your game" toast.
//
// Pure static apart from LocalTag: the decisions and the filter building run headlessly in
// NetProtocolTests.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Utility;
using Unity.Netcode;
using Unity.Services.Multiplayer;

namespace CosmicShore.Gameplay
{
    /// <summary>What a remote build's published protocol means for playing with this build.</summary>
    public enum ProtocolCompatibility
    {
        /// <summary>The remote publishes the same protocol as this build: they can play together.</summary>
        Same        = 0,
        /// <summary>The remote publishes a different protocol: Netcode would refuse the join.</summary>
        Different   = 1,
        /// <summary>The remote publishes no protocol at all: a build from before this gate, so incompatible.</summary>
        Unpublished = 2,
        /// <summary>This build could not read its own protocol (no NetworkManager): never matched with anyone.</summary>
        LocalUnknown = 3,
        /// <summary>There was nobody to compare against (an empty session): the join decides on its own.</summary>
        NoPeers     = 4,
    }

    /// <summary>A joined session's members run a different protocol (or publish none). Carries the facts for the authored message.</summary>
    public sealed class ProtocolMismatchException : Exception
    {
        /// <summary>This build's protocol tag.</summary>
        public string LocalVersion { get; }
        /// <summary>The other build's protocol tag; empty when it published none (an older build).</summary>
        public string RemoteVersion { get; }
        /// <summary>The display name of the member whose version differs; empty when unknown.</summary>
        public string RemoteName { get; }
        public ProtocolCompatibility Verdict { get; }

        public ProtocolMismatchException(ProtocolCompatibility verdict, string localVersion, string remoteVersion, string remoteName)
            : base($"Protocol mismatch ({verdict}): this build is {localVersion}, the session's member '{remoteName}' is " +
                   $"{(string.IsNullOrEmpty(remoteVersion) ? "unpublished" : remoteVersion)}.")
        {
            Verdict       = verdict;
            LocalVersion  = localVersion ?? string.Empty;
            RemoteVersion = remoteVersion ?? string.Empty;
            RemoteName    = remoteName ?? string.Empty;
        }
    }

    public static class NetProtocol
    {
        /// <summary>The session-property and player-property key every build from this gate on publishes.</summary>
        public const string PropertyKey = "protocol";

        /// <summary>The indexed slot the session property sits in (String1 is the game mode, String2 the seat count).</summary>
        public const PropertyIndex SessionIndex = PropertyIndex.String3;

        /// <summary>The query field that reads <see cref="SessionIndex"/>.</summary>
        public const FilterField FilterIndex = FilterField.StringIndex3;

        /// <summary>Joins a game mode to its protocol in the String1 value: "PRESENCE_LOBBY@p24".</summary>
        public const string ScopeSeparator = "@p";

        /// <summary>The tag a build that cannot read its own NetworkConfig publishes and scopes under.</summary>
        public const string UnknownLocalTag = "unknown";

        static string s_lastRead;
        static bool   s_missingLogged;

        /// <summary>
        /// This build's protocol, read from the live NetworkManager's <c>NetworkConfig.ProtocolVersion</c> - the
        /// value the bump rule (Docs/claude/MULTIPLAYER_AND_SOCIAL.md § Multiplayer / Netcode) raises on the prefab.
        /// Without a NetworkManager it returns the last value read, or <see cref="UnknownLocalTag"/> (logged once as an
        /// error: a session made then is matched with nobody, never with the wrong build).
        /// </summary>
        public static string LocalTag
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.NetworkConfig != null)
                    return s_lastRead = Format(nm.NetworkConfig.ProtocolVersion);
                if (!string.IsNullOrEmpty(s_lastRead)) return s_lastRead;
                if (!s_missingLogged)
                {
                    s_missingLogged = true;
                    CSDebug.LogError("[NetProtocol] No NetworkManager to read NetworkConfig.ProtocolVersion from - sessions made now are " +
                                     $"tagged '{UnknownLocalTag}' and match nobody.");
                }
                return UnknownLocalTag;
            }
        }

        /// <summary>The published form of a protocol version (invariant decimal).</summary>
        public static string Format(ushort protocolVersion) => protocolVersion.ToString(CultureInfo.InvariantCulture);

        /// <summary>A game-mode value scoped to one protocol: <c>Scope("PRESENCE_LOBBY", "24") == "PRESENCE_LOBBY@p24"</c>.</summary>
        public static string Scope(string baseValue, string protocol) => (baseValue ?? string.Empty) + ScopeSeparator + (protocol ?? string.Empty);

        /// <summary>
        /// The compatibility of a remote build that published <paramref name="remote"/> (null or empty: published nothing)
        /// with this build's <paramref name="local"/>. Only <see cref="ProtocolCompatibility.Same"/> may play together.
        /// </summary>
        public static ProtocolCompatibility Decide(string local, string remote)
        {
            if (string.IsNullOrEmpty(local) || local == UnknownLocalTag) return ProtocolCompatibility.LocalUnknown;
            if (string.IsNullOrEmpty(remote)) return ProtocolCompatibility.Unpublished;
            return string.Equals(local, remote, StringComparison.Ordinal) ? ProtocolCompatibility.Same : ProtocolCompatibility.Different;
        }

        /// <summary>True only when the two builds can play together.</summary>
        public static bool IsCompatible(string local, string remote) => Decide(local, remote) == ProtocolCompatibility.Same;

        /// <summary>
        /// The verdict on a session just joined, from the protocol each OTHER member publishes (null or empty for a
        /// member that publishes none). Every member must match; the first that does not decides the verdict and is
        /// reported through <paramref name="mismatchIndex"/> (-1 when none). No members at all is
        /// <see cref="ProtocolCompatibility.NoPeers"/>: nothing to compare, the join's own outcome decides.
        /// </summary>
        public static ProtocolCompatibility DecideAgainstMembers(string local, IReadOnlyList<string> memberProtocols, out int mismatchIndex)
        {
            mismatchIndex = -1;
            if (string.IsNullOrEmpty(local) || local == UnknownLocalTag) return ProtocolCompatibility.LocalUnknown;
            if (memberProtocols == null || memberProtocols.Count == 0) return ProtocolCompatibility.NoPeers;
            for (int i = 0; i < memberProtocols.Count; i++)
            {
                var verdict = Decide(local, memberProtocols[i]);
                if (verdict == ProtocolCompatibility.Same) continue;
                mismatchIndex = i;
                return verdict;
            }
            return ProtocolCompatibility.Same;
        }

        /// <summary>The indexed session property every session this game creates carries.</summary>
        public static SessionProperty SessionProperty(string protocol) =>
            new SessionProperty(protocol ?? string.Empty, VisibilityPropertyOptions.Public, SessionIndex);

        /// <summary>The player property a party-session member publishes, read by a joiner (<see cref="DecideAgainstMembers"/>).</summary>
        public static PlayerProperty PlayerProperty(string protocol) =>
            new PlayerProperty(protocol ?? string.Empty, VisibilityPropertyOptions.Public);

        /// <summary>
        /// The matchmaking query: the protocol-scoped game mode (String1), the seat count (String2) and the protocol
        /// itself (String3). An old build's session has a bare game mode and no String3, so it matches neither.
        /// </summary>
        public static List<FilterOption> MatchFilters(string gameMode, string maxPlayers, string protocol) => new()
        {
            new FilterOption(FilterField.StringIndex1, Scope(gameMode, protocol), FilterOperation.Equal),
            new FilterOption(FilterField.StringIndex2, maxPlayers ?? string.Empty, FilterOperation.Equal),
            new FilterOption(FilterIndex,              protocol ?? string.Empty,  FilterOperation.Equal),
        };

        /// <summary>The matchmaking session's properties: the same three values <see cref="MatchFilters"/> asks for.</summary>
        public static Dictionary<string, SessionProperty> MatchSessionProperties(
            string gameModeKey, string gameMode, string maxPlayersKey, string maxPlayers, string protocol) => new()
        {
            { gameModeKey,   new SessionProperty(Scope(gameMode, protocol), VisibilityPropertyOptions.Public, PropertyIndex.String1) },
            { maxPlayersKey, new SessionProperty(maxPlayers ?? string.Empty, VisibilityPropertyOptions.Public, PropertyIndex.String2) },
            { PropertyKey,   SessionProperty(protocol) },
        };

        /// <summary>
        /// The presence-lobby query, used by BOTH the join and the converge so the two always see the same set: the
        /// protocol-scoped presence game mode (String1) and the protocol (String3).
        /// </summary>
        public static List<FilterOption> PresenceFilters(string presenceGameMode, string protocol) => new()
        {
            new FilterOption(FilterField.StringIndex1, Scope(presenceGameMode, protocol), FilterOperation.Equal),
            new FilterOption(FilterIndex,              protocol ?? string.Empty,          FilterOperation.Equal),
        };

        /// <summary>The presence lobby's properties: the same two values <see cref="PresenceFilters"/> asks for.</summary>
        public static Dictionary<string, SessionProperty> PresenceSessionProperties(string gameModeKey, string presenceGameMode, string protocol) => new()
        {
            { gameModeKey, new SessionProperty(Scope(presenceGameMode, protocol), VisibilityPropertyOptions.Public, PropertyIndex.String1) },
            { PropertyKey, SessionProperty(protocol) },
        };
    }
}
