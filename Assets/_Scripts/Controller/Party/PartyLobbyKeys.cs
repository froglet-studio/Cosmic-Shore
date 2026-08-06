// ─────────────────────────────────────────────────────────────────────────────
// PartyLobbyKeys.cs
// The wire format of the party/presence system: every UGS player-property and
// session-property key, in one place.
//
// WHY this class exists:
//   These twelve string literals were declared independently in five files -
//   HostConnectionService, PresenceLobbyService, PartySessionService,
//   PartyMemberService and AcceptanceSignalService - with no shared owner. Each
//   file redeclared the subset it happened to need, so the wire format had five
//   partial definitions and nothing that could be read to learn it.
//
//   That is not a hypothetical cost. `presenceState` exists in exactly ONE of
//   those files, which is why a converge migration silently dropped it (the
//   rejoin rebuilt the record from a builder that had never heard of the key)
//   and peers rendered "CONNECTING…" forever - Docs/PresenceSystem/BUGS.md B11
//   and B14. A key that only one writer knows about is a key the other writers
//   will drop.
//
// SCOPE - this file is the FORMAT, not the policy:
//   It says what the keys are called. It says nothing about who may write them,
//   when, or what the values mean. HostConnectionService remains the single
//   writer of presence player-properties; the ownership rules live in
//   Docs/PartySystem/ARCHITECTURE.md.
//
// LIFETIME / THREAD SAFETY:
//   Static constants. No state, no thread affinity.
// ─────────────────────────────────────────────────────────────────────────────

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every UGS property key used by the party and presence systems.
    ///
    /// <para>
    /// Consumers declare a local <c>private const</c> that forwards here when a
    /// test reflects on the constant by name (see
    /// <see cref="HostConnectionService"/>); otherwise they reference these
    /// members directly.
    /// </para>
    /// </summary>
    public static class PartyLobbyKeys
    {
        // ── Identity ─────────────────────────────────────────────────────────

        /// <summary>Player's display name. Written to BOTH sessions.</summary>
        public const string DisplayName = "displayName";

        /// <summary>Player's profile-icon index. Written to BOTH sessions.</summary>
        public const string AvatarId = "avatarId";

        // ── Advertised party state (presence lobby only) ─────────────────────
        //
        // A HINT about a party you are not in. For your OWN party read
        // IPartyRoster instead - see PartyPlayerData.AdvertisedPartyMemberCount
        // for why that distinction is load-bearing.

        /// <summary>Advertised party size, including the advertiser.</summary>
        public const string PartyCount = "partyCount";

        /// <summary>Advertised maximum party slots.</summary>
        public const string PartyMax = "partyMax";

        /// <summary>Active match name, or empty while in the menu.</summary>
        public const string MatchName = "matchName";

        /// <summary>
        /// The advertiser's <c>PresenceState</c> as an int.
        ///
        /// <para>
        /// Absent means "unknown, assume in-world" - never Offline. A peer on a
        /// build from before this key existed publishes nothing, and reading
        /// that as 0 would make every such player invisible.
        /// </para>
        /// </summary>
        public const string PresenceState = "presenceState";

        // ── Invite handshake (presence lobby only) ───────────────────────────

        /// <summary>
        /// The sender's outgoing invites, newline-separated, one line per
        /// target. Line format is owned by <see cref="InviteService"/>.
        /// </summary>
        public const string InvitePayloads = "invite_payloads";

        /// <summary>
        /// A guest's "I am in session X" advertisement, carrying the Relay
        /// session id. Published by guests only; the host's admit scan reads it,
        /// cross-checked against the authoritative session roster
        /// (Docs/PartySystem/BUGS.md B8).
        /// </summary>
        public const string JoinedParty = "joined_party";

        /// <summary>
        /// The accepting player's acknowledgement, carrying the host's PlayerId.
        /// Deliberately never preserved across a lobby rejoin - it is a
        /// fast-path hint the session member sync also provides, and carrying it
        /// over would make a stale signal permanent.
        /// </summary>
        public const string AcceptedInvite = "accepted_invite";

        // ── Session-level (presence lobby discovery) ─────────────────────────

        /// <summary>Session-property key used to find the presence lobby.</summary>
        public const string GameMode = "gameMode";

        /// <summary>Session-property VALUE that marks the presence lobby.</summary>
        public const string PresenceLobbyGameMode = "PRESENCE_LOBBY";

        // ── Sentinels ────────────────────────────────────────────────────────

        /// <summary>
        /// Placeholder session id in an invite payload written before the real
        /// Relay id is known.
        /// </summary>
        public const string PendingSessionId = "PENDING";
    }
}
