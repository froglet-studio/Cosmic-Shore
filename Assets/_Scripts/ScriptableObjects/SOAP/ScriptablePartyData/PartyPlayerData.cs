using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Immutable snapshot of a player's identity within the party/presence system.
    /// Used as the payload for SOAP events and as the element type for ScriptableList.
    /// Equality is by PlayerId only — optional party-state fields can update without
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

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public int AvatarId => avatarId;

        /// <summary>Members currently in this player's party (includes themselves).</summary>
        public int PartyMemberCount => partyMemberCount;

        /// <summary>Max party slots advertised by this player (0 if unknown).</summary>
        public int PartyMaxSlots => partyMaxSlots;

        /// <summary>Active match name if this player is in-game, else empty.</summary>
        public string MatchName => matchName ?? string.Empty;

        public PartyPlayerData(string playerId, string displayName, int avatarId)
            : this(playerId, displayName, avatarId, 0, 0, null) { }

        public PartyPlayerData(
            string playerId,
            string displayName,
            int avatarId,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName)
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.avatarId = avatarId;
            this.partyMemberCount = partyMemberCount;
            this.partyMaxSlots = partyMaxSlots;
            this.matchName = matchName;
        }

        public override bool Equals(object obj)
        {
            if (obj is not PartyPlayerData other) return false;
            return playerId == other.playerId;
        }

        public override int GetHashCode() => playerId?.GetHashCode() ?? 0;
    }
}
