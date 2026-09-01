using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Data payload for a party invite received from another player.
    /// Used as the SOAP event payload for invite notifications.
    ///
    /// NAMING NOTE (invite chain): despite their names, the "host" fields
    /// identify the SENDER of the invite, who is not necessarily the party's
    /// session host - a party MEMBER can invite too (B, a guest in A's party,
    /// invites C). <see cref="PartySessionId"/> is always the sender's
    /// CURRENT party session - the real join target - so the acceptor lands
    /// in the correct party regardless of the sender's role; the true host
    /// enters the acceptor's roster via the first session member sync.
    /// See Docs/PartySystem/INVITE_ENHANCEMENTS.md Task 4.
    /// </summary>
    [System.Serializable]
    public struct PartyInviteData
    {
        [SerializeField] private string hostPlayerId;
        [SerializeField] private string partySessionId;
        [SerializeField] private string hostDisplayName;
        [SerializeField] private int hostAvatarId;

        /// <summary>The invite SENDER's player id (host or party member).</summary>
        public string HostPlayerId => hostPlayerId;
        /// <summary>The sender's current party session id - the join target.</summary>
        public string PartySessionId => partySessionId;
        /// <summary>The invite SENDER's display name (shown as "X invited you").</summary>
        public string HostDisplayName => hostDisplayName;
        /// <summary>The invite SENDER's avatar id.</summary>
        public int HostAvatarId => hostAvatarId;

        public PartyInviteData(string hostPlayerId, string partySessionId, string hostDisplayName, int hostAvatarId)
        {
            this.hostPlayerId = hostPlayerId;
            this.partySessionId = partySessionId;
            this.hostDisplayName = hostDisplayName;
            this.hostAvatarId = hostAvatarId;
        }
    }
}
