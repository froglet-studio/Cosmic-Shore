using CosmicShore.Utilities.Network;
using UnityEngine;


namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// This struct is used to store player data for the current session.
    /// </summary>
    public struct SessionPlayerData : ISessionPlayerData
    {
        public string PlayerName;
        public int PlayerNumber;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;

        public bool IsConnected { get; set; }
        public ulong ClientID { get; set; }

        // Instead of using a NetworkGuid (two ulongs), we could just use an int or even a byte-sized index into an array of possible avatars
        // defined in our game data source.
        public NetworkGuid EntityNetworkGuid;
        public bool HasCharacterSpawned;

        public SessionPlayerData(ulong clientID, string name, NetworkGuid avatarNetworkGuid, bool isConnected = false, bool hasCharacterSpawned = false)
        {
            ClientID = clientID;
            PlayerName = name;
            PlayerNumber = -1;
            PlayerPosition = Vector3.zero;
            PlayerRotation = Quaternion.identity;
            EntityNetworkGuid = avatarNetworkGuid;
            IsConnected = isConnected;
            HasCharacterSpawned = hasCharacterSpawned;
        }

        public void Reinitialize()
        {
            HasCharacterSpawned = false;
        }
    }

}