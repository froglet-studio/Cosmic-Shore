using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Immutable snapshot of a player's identity within the party/presence system.
    /// Used as the payload for SOAP events and as the element type for ScriptableList.
    /// Equality is by PlayerId only - optional party-state fields can update without
    /// breaking list dedup.
    /// </summary>
    [System.Serializable]
    public struct PartyPlayerData
    {
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;
        [SerializeField] private int avatarId;

        [SerializeField] private int partyMemberCount;
        [SerializeField] private int partyMaxSlots;
        [SerializeField] private string matchName;
        [SerializeField] private string partySessionId;

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public int AvatarId => avatarId;

        /// <summary>Members currently in this player's party (includes themselves).</summary>
        public int PartyMemberCount => partyMemberCount;

        /// <summary>Max party slots advertised by this player (0 if unknown).</summary>
        public int PartyMaxSlots => partyMaxSlots;

        /// <summary>Active match name if this player is in-game, else empty.</summary>
        public string MatchName => matchName ?? string.Empty;

        /// <summary>
        /// The UGS session id of the Relay party this player is currently in (host or guest),
        /// published on their presence-lobby row. It is what a direct JOIN and a SPECTATE both
        /// need - the party session IS the game session (MultiplayerSetup reuses it at launch),
        /// so one id serves both. Empty when the player has no joinable session: not yet
        /// created, offline, or themselves a spectator in somebody else's match (a spectator
        /// deliberately publishes no session so nobody can chain-spectate through them).
        /// </summary>
        public string PartySessionId => partySessionId ?? string.Empty;

        /// <summary>True when the player advertises a session another pilot could join.</summary>
        public bool HasJoinableSession => !string.IsNullOrEmpty(partySessionId);

        /// <summary>True when the player is in a live multiplayer match (a non-empty match name).</summary>
        public bool IsInMatch => !string.IsNullOrEmpty(matchName);

        public PartyPlayerData(string playerId, string displayName, int avatarId)
            : this(playerId, displayName, avatarId, 0, 0, null, null) { }

        public PartyPlayerData(
            string playerId,
            string displayName,
            int avatarId,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName)
            : this(playerId, displayName, avatarId, partyMemberCount, partyMaxSlots, matchName, null) { }

        public PartyPlayerData(
            string playerId,
            string displayName,
            int avatarId,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName,
            string partySessionId)
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.avatarId = avatarId;
            this.partyMemberCount = partyMemberCount;
            this.partyMaxSlots = partyMaxSlots;
            this.matchName = matchName;
            this.partySessionId = partySessionId;
        }

        public override bool Equals(object obj)
        {
            if (obj is not PartyPlayerData other) return false;
            return playerId == other.playerId;
        }

        public override int GetHashCode() => playerId?.GetHashCode() ?? 0;
    }
}
