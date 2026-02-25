using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Data payload for a party invite received from another player.
    /// Used as the SOAP event payload for invite notifications.
    /// </summary>
    [System.Serializable]
    public struct PartyInviteData
    {
        [SerializeField] private string hostPlayerId;
        [SerializeField] private string partySessionId;
        [SerializeField] private string hostDisplayName;
        [SerializeField] private int hostAvatarId;

        public string HostPlayerId => hostPlayerId;
        public string PartySessionId => partySessionId;
        public string HostDisplayName => hostDisplayName;
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
