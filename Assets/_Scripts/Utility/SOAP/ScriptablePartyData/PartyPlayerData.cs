using UnityEngine;

namespace CosmicShore.Utility.SOAP.ScriptablePartyData
{
    /// <summary>
    /// Immutable snapshot of a player's identity within the party/presence system.
    /// Used as the payload for SOAP events and as the element type for ScriptableList.
    /// </summary>
    [System.Serializable]
    public struct PartyPlayerData
    {
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;
        [SerializeField] private int avatarId;

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public int AvatarId => avatarId;

        public PartyPlayerData(string playerId, string displayName, int avatarId)
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.avatarId = avatarId;
        }

        public override bool Equals(object obj)
        {
            if (obj is not PartyPlayerData other) return false;
            return playerId == other.playerId;
        }

        public override int GetHashCode() => playerId?.GetHashCode() ?? 0;
    }
}
