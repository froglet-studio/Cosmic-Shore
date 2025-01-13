using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;

namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// Data for a local lobby user interface. This will update data and is observerd to know when to push local user changes to the entire lobby.
    /// </summary>
    [Serializable]
    public class LocalLobbyUser
    {
        /// <summary>
        /// This struct holds the data for a local lobby user.
        /// It is used to update the data and is observed to know when to push local user changes to the entire lobby.
        /// </summary>
        public struct UserData
        {
            public bool IsHost { get; set; }

            /// <summary>
            /// The name with which this player is signed in to the game.
            /// </summary>
            public string PlayerName { get; set; }

            /// <summary>
            /// The unique ID with which the player signed in to the game.
            /// </summary>
            public string ID { get; set; }
            public UserData(bool isHost, string displayName, string id)
            {
                IsHost = isHost;
                PlayerName = displayName;
                ID = id;
            }
        }

        private UserData _userData;

        /// <summary>
        /// Used for limiting costly OnChanged actions to just the members which actually changed.
        /// </summary>
        [Flags]
        public enum UserMember
        {
            IsHost = 1,
            PlayerName = 2,             // The name with which this player is signed in to the game.
            ID = 4,                     // The unique ID with which the player signed in to the game.
        }

        private UserMember _lastChangedUserMember;

        public event Action<LocalLobbyUser> OnChanged;

        public LocalLobbyUser()
        {
            _userData = new UserData(isHost: false, displayName: null, id: null);
        }

        public void ResetState()
        {
            _userData = new UserData(false, _userData.PlayerName, _userData.ID);
        }

        public bool IsHost
        {
            get { return _userData.IsHost; }
            set
            {
                if (_userData.IsHost != value)
                {
                    _userData.IsHost = value;
                    _lastChangedUserMember = UserMember.IsHost;
                    OnChanged?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// The name with which this player is signed in to the game.
        /// </summary>
        public string PlayerName
        {
            get => _userData.PlayerName;
            set
            {
                if (_userData.PlayerName != value)
                {
                    _userData.PlayerName = value;
                    _lastChangedUserMember = UserMember.PlayerName;
                    OnChanged?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// The unique ID with which the player signed in to the game.
        /// </summary>
        public string ID
        {
            get => _userData.ID;
            set
            {
                if (_userData.ID != value)
                {
                    _userData.ID = value;
                    _lastChangedUserMember = UserMember.ID;
                    OnChanged?.Invoke(this);
                }
            }
        }

        public void CopyDataFrom(LocalLobbyUser lobby)
        {
            UserData data = lobby._userData;
            int lastChanged = // Set flags just for the members that will be changed.
                (_userData.IsHost == data.IsHost ? 0 : (int)UserMember.IsHost) |
                (_userData.PlayerName == data.PlayerName ? 0 : (int)UserMember.PlayerName) |
                (_userData.ID == data.ID ? 0 : (int)UserMember.ID);

            if (lastChanged == 0)
                return;

            _userData = data;
            _lastChangedUserMember = (UserMember)lastChanged;

            OnChanged?.Invoke(this);
        }

        public Dictionary<string, PlayerDataObject> GetDataForUnityServices() =>
            new Dictionary<string, PlayerDataObject>()
            {
                {"DisplayName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, PlayerName) }
            };
    }
}
