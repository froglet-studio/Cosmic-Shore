using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Player = Unity.Services.Lobbies.Models.Player;


namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// A local wrapper around a lobby's remote data, with additional functionality for providing that data to UI elements and tracking local player objects.
    /// </summary>
    [Serializable]
    public sealed class LocalLobby
    {
        public struct LobbyDataStruct
        {
            public string LobbyID { get; set; }
            public string LobbyCode { get; set; }
            public string RelayJoinCode { get; set; }
            public string LobbyName { get; set; }
            public bool IsPrivate { get; set; }
            public int MaxPlayerCount { get; set; }

            public LobbyDataStruct(LobbyDataStruct existing)
            {
                LobbyID = existing.LobbyID;
                LobbyCode = existing.LobbyCode;
                RelayJoinCode = existing.RelayJoinCode;
                LobbyName = existing.LobbyName;
                IsPrivate = existing.IsPrivate;
                MaxPlayerCount = existing.MaxPlayerCount;
            }

            public LobbyDataStruct(string lobbyCode)
            {
                LobbyID = null;
                LobbyCode = lobbyCode;
                RelayJoinCode = null;
                LobbyName = null;
                IsPrivate = false;
                MaxPlayerCount = -1;
            }
        }

        public event Action<LocalLobby> OnChanged;

        private LobbyDataStruct _lobbyData;
        public LobbyDataStruct LobbyData => new LocalLobby.LobbyDataStruct(_lobbyData);

        private Dictionary<string, LocalLobbyUser> _localLobbyUsers = new Dictionary<string, LocalLobbyUser>();
        public Dictionary<string, LocalLobbyUser> LocalLobbyUsers => _localLobbyUsers;

        public string LobbyID
        {
            get => _lobbyData.LobbyID;
            set
            {
                _lobbyData.LobbyID = value;
                OnChanged?.Invoke(this);
            }
        }

        public string LobbyCode
        {
            get => _lobbyData.LobbyCode;
            set
            {
                _lobbyData.LobbyCode = value;
                OnChanged?.Invoke(this);
            }
        }

        public string RelayJoinCode
        {
            get => _lobbyData.RelayJoinCode;
            set
            {
                _lobbyData.RelayJoinCode = value;
                OnChanged?.Invoke(this);
            }
        }

        public string LobbyName
        {
            get => _lobbyData.LobbyName;
            set
            {
                _lobbyData.LobbyName = value;
                OnChanged?.Invoke(this);
            }
        }

        public bool Private
        {
            get => _lobbyData.IsPrivate;
            set
            {
                _lobbyData.IsPrivate = value;
                OnChanged?.Invoke(this);
            }
        }

        public int MaxPlayerCount
        {
            get => _lobbyData.MaxPlayerCount;
            set
            {
                _lobbyData.MaxPlayerCount = value;
                OnChanged?.Invoke(this);
            }
        }

        public int PlayerCount => _localLobbyUsers.Count;

        /// <summary>
        /// Create a list of new LocalLobbies from the result of a lobby list query.
        /// </summary>
        public static List<LocalLobby> CreateLocalLobbies(QueryResponse response)
        {
            List<LocalLobby> localLobbyList = new List<LocalLobby>();
            foreach (Lobby lobby in response.Results)
            {
                localLobbyList.Add(Create(lobby));
            }
            return localLobbyList;
        }


        /// <summary>
        /// Create a new LocalLobby from a remote lobby object.
        /// </summary>
        public static LocalLobby Create(Lobby lobby)
        {
            LocalLobby localLobby = new LocalLobby();
            localLobby.ApplyRemoteData(lobby);
            return localLobby;
        }

        /// <summary>
        /// Add a new user to the local lobby.
        /// </summary>
        public void AddUser(LocalLobbyUser localLobbyUser)
        {
            if (!_localLobbyUsers.ContainsKey(localLobbyUser.ID))
            {
                DoAddUser(localLobbyUser);
                OnChanged?.Invoke(this);
            }
        }

        /// <summary>
        /// Remove a user from the local lobby.
        /// </summary>
        public void RemoveUser(LocalLobbyUser localLobbyUser)
        {
            DoRemoveUser(localLobbyUser);
            OnChanged?.Invoke(this);
        }

        /// <summary>
        /// Apply the data from a remote lobby object to the local lobby.
        /// </summary>
        public void ApplyRemoteData(Lobby lobby)
        {
            LobbyDataStruct lobbyData = new LobbyDataStruct();  // Technically, this is largely redundant after the first assignment, but it won't do any harm to assign it again.
            lobbyData.LobbyID = lobby.Id;
            lobbyData.LobbyCode = lobby.LobbyCode;
            lobbyData.IsPrivate = lobby.IsPrivate;
            lobbyData.LobbyName = lobby.Name;
            lobbyData.MaxPlayerCount = lobby.MaxPlayers;

            if (lobby.Data != null)
            {
                // By providing RelayCode through the lobby data with Member visibility, we ensure a client is connected to the lobby before they could attempt a relay connection,
                // preventing timing issues between them.
                lobbyData.RelayJoinCode = lobby.Data.ContainsKey("RelayJoinCode") ? lobby.Data["RelayJoinCode"].Value : null;
            }
            else
            {
                lobbyData.RelayJoinCode = null;
            }

            Dictionary<string, LocalLobbyUser> localLobbyUsers = new();
            foreach (Player player in lobby.Players)
            {
                if (player.Data != null)
                {
                    if (LocalLobbyUsers.ContainsKey(player.Id))
                    {
                        localLobbyUsers.Add(player.Id, LocalLobbyUsers[player.Id]);
                        continue;
                    }
                }

                // If the player isn't connected to Relay, get the most recent data that the lobby knows.
                // (If we haven't seen this player yet, a new local representation of the player will have already been added by the LocalLobby.)
                LocalLobbyUser localLobbyUser = new()
                {
                    IsHost = lobby.HostId.Equals(player.Id),
                    PlayerName = player.Data != null && player.Data.ContainsKey("DisplayName") ? player.Data["DisplayName"].Value : default,
                    ID = player.Id
                };

                localLobbyUsers.Add(localLobbyUser.ID, localLobbyUser);
            }

            CopyDataFrom(lobbyData, localLobbyUsers);
        }

        /// <summary>
        /// Reset the local lobby to a new state with a single user.
        /// </summary>
        public void Reset(LocalLobbyUser localLobbyUser)
        {
            CopyDataFrom(new LobbyDataStruct(), new Dictionary<string, LocalLobbyUser>());
            AddUser(localLobbyUser);
        }

        /// <summary>
        /// Get the data for Unity Services to send to the server.
        /// </summary>
        internal Dictionary<string, DataObject> GetDataForUnityServices() =>
           new Dictionary<string, DataObject>
           {
                { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) }
           };

        private void CopyDataFrom(LobbyDataStruct lobbyData, Dictionary<string, LocalLobbyUser> currentLocalLobbyUsers)
        {
            _lobbyData = lobbyData;

            if (currentLocalLobbyUsers == null)
            {
                _localLobbyUsers = new Dictionary<string, LocalLobbyUser>();
            }
            else
            {
                List<LocalLobbyUser> localLobbyUserToRemove = new();
                foreach (KeyValuePair<string, LocalLobbyUser> oldLocalLobbyUser in _localLobbyUsers)
                {
                    if (currentLocalLobbyUsers.ContainsKey(oldLocalLobbyUser.Key))
                    {
                        oldLocalLobbyUser.Value.CopyDataFrom(currentLocalLobbyUsers[oldLocalLobbyUser.Key]);
                    }
                    else
                    {
                        localLobbyUserToRemove.Add(oldLocalLobbyUser.Value);
                    }
                }

                foreach (LocalLobbyUser localLobbyUser in localLobbyUserToRemove)
                {
                    DoRemoveUser(localLobbyUser);
                }

                foreach (KeyValuePair<string, LocalLobbyUser> newLocalLobbyUser in currentLocalLobbyUsers)
                {
                    if (!_localLobbyUsers.ContainsKey(newLocalLobbyUser.Key))
                    {
                        DoAddUser(newLocalLobbyUser.Value);
                    }
                }
            }

            OnChanged?.Invoke(this);
        }

        private void DoAddUser(LocalLobbyUser localLobbyUser)
        {
            _localLobbyUsers.Add(localLobbyUser.ID, localLobbyUser);
            localLobbyUser.OnChanged += OnLocalLobbyUserChanged;
        }

        private void DoRemoveUser(LocalLobbyUser localLobbyUser)
        {
            if (localLobbyUser.ID == null)
                return;

            if (!_localLobbyUsers.ContainsKey(localLobbyUser.ID))
            {
                Debug.LogWarning($"Player {localLobbyUser.PlayerName}({localLobbyUser.ID}) does not exist in lobby: { LobbyID}");
                return;
            }

            _localLobbyUsers.Remove(localLobbyUser.ID);
            localLobbyUser.OnChanged -= OnLocalLobbyUserChanged;
        }

        private void OnLocalLobbyUserChanged(LocalLobbyUser user)
        {
            OnChanged?.Invoke(this);
        }
    }
}