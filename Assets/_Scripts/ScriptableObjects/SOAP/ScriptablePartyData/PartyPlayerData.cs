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
        [SerializeField] private int presenceState;

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
        /// This player's advertised <c>PresenceState</c> as an int.
        ///
        /// <para>
        /// Defaults to <c>(int)PresenceState.Present</c>, NOT 0. A peer running a
        /// build from before the presence property existed publishes nothing, and
        /// 0 is <c>Offline</c> - so defaulting to 0 would make every such player
        /// invisible. Absent means "unknown, assume they are in the world".
        /// </para>
        /// </summary>
        public int PresenceState => presenceState;

        /// <summary>
        /// True when this player is actually in the world - their vessel exists.
        /// False only while they are positively known to be still joining, i.e.
        /// they published Joining or Announced.
        /// </summary>
        public bool IsInWorld => presenceState >= PRESENCE_PRESENT;

        /// <summary>
        /// Mirrors <c>PresenceState.Present</c>. Kept as a local literal because
        /// this struct lives in CosmicShore.ScriptableObjects and the enum lives
        /// in CosmicShore.Gameplay; the wire format is an int either way.
        /// </summary>
        public const int PRESENCE_PRESENT = 3;

        public PartyPlayerData(string playerId, string displayName, int avatarId)
            : this(playerId, displayName, avatarId, 0, 0, null, PRESENCE_PRESENT) { }

        public PartyPlayerData(
            string playerId,
            string displayName,
            int avatarId,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName)
            : this(playerId, displayName, avatarId, partyMemberCount, partyMaxSlots, matchName, PRESENCE_PRESENT) { }

        public PartyPlayerData(
            string playerId,
            string displayName,
            int avatarId,
            int partyMemberCount,
            int partyMaxSlots,
            string matchName,
            int presenceState)
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.avatarId = avatarId;
            this.partyMemberCount = partyMemberCount;
            this.partyMaxSlots = partyMaxSlots;
            this.matchName = matchName;
            this.presenceState = presenceState;
        }

        /// <summary>
        /// Identity only - deliberately keyed on <see cref="PlayerId"/> so the
        /// optional state fields can change without breaking ScriptableList
        /// dedup. Use <see cref="HasSameDisplayDataAs"/> to ask whether a row
        /// needs repainting.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is not PartyPlayerData other) return false;
            return playerId == other.playerId;
        }

        public override int GetHashCode() => playerId?.GetHashCode() ?? 0;

        /// <summary>
        /// True when every field a roster row RENDERS from is identical - i.e.
        /// replacing the stored entry with <paramref name="other"/> would change
        /// nothing on screen.
        ///
        /// <para>
        /// <b>This is the single list of render-relevant fields. Add new ones
        /// here.</b> The diff in <c>HostConnectionService.RefreshOnlinePlayersDiff</c>
        /// used to inline this comparison, and when <see cref="PresenceState"/>
        /// was added to this struct and to the property reader, the inline
        /// comparison was not updated. The consequence was invisible and total: a
        /// peer's row was created while they were still `Announced`, they
        /// published `Present` a moment later, the diff read the new value,
        /// compared the five fields it knew about, concluded "unchanged", and
        /// never replaced the entry - so every peer rendered them as
        /// "CONNECTING…" permanently. Keeping the comparison next to the fields
        /// makes that omission structurally impossible to repeat.
        /// </para>
        /// </summary>
        public bool HasSameDisplayDataAs(PartyPlayerData other) =>
            displayName      == other.displayName      &&
            avatarId         == other.avatarId         &&
            partyMemberCount == other.partyMemberCount &&
            partyMaxSlots    == other.partyMaxSlots    &&
            MatchName        == other.MatchName        &&
            presenceState    == other.presenceState;
    }
}
