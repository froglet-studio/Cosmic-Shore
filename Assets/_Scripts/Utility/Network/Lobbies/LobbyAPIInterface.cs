using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;


namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// Wrapper for all the interactions with the Lobby API.
    /// </summary>
    public class LobbyAPIInterface
    {
        private const int MAX_LOBBIES_TO_SHOW = 16; // If more are necessary, consider retrieving paginated results or using filters.

        private readonly List<QueryFilter> _querryFilters;
        private readonly List<QueryOrder> _queryOrders;

        public LobbyAPIInterface()
        {
            // Filter for open lobbies only
            _querryFilters = new List<QueryFilter>()
            {
                new(field: QueryFilter.FieldOptions.AvailableSlots,
                op: QueryFilter.OpOptions.GT,
                value: "0")
            };

            // Order by newest lobbies first.
            _queryOrders = new List<QueryOrder>()
            {
                new(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };
        }

        public async Task<Lobby> CreateLobby(string requesterUasId, string lobbyName, int maxPlayers, bool isPrivate, Dictionary<string, PlayerDataObject> hostUserData, Dictionary<string, DataObject> lobbyData)
        {
            CreateLobbyOptions createLobbyOptions = new()
            {
                IsPrivate = isPrivate,
                IsLocked = true,    // locking the lobby at creation to prevent other players from joining before it is ready
                Player = new Player(id: requesterUasId, data: hostUserData),
                Data = lobbyData
            };

            return await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
        }

        public async Task DeleteLobby(string lobbyId)
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
        }

        public async Task<Lobby> JoinLobbyByCode(string requestedUasId, string lobbyCode, Dictionary<string, PlayerDataObject> localUserData)
        {
            JoinLobbyByCodeOptions joinOptions = new() { Player = new Player(id: requestedUasId, data: localUserData) };
            return await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
        }

        public async Task<Lobby> JoinLobbyById(string requestedUasId, string lobbyId, Dictionary<string, PlayerDataObject> localUserData)
        {
            JoinLobbyByIdOptions joinOptions = new () { Player = new Player(id: requestedUasId, data: localUserData) };
            return await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
        }

        public async Task<Lobby> QuickJoinLobby(string requestedUasId, Dictionary<string, PlayerDataObject> localUserData)
        {
            QuickJoinLobbyOptions quickJoinLobbyOptions = new()
            {
                Filter = _querryFilters,
                Player = new Player(id: requestedUasId, data: localUserData),
            };

            return await LobbyService.Instance.QuickJoinLobbyAsync(quickJoinLobbyOptions);
        }

        public async Task<Lobby> ReconnectToLobby(string lobbyId)
        {
            return await LobbyService.Instance.ReconnectToLobbyAsync(lobbyId);
        }

        public async Task RemovePlayerFromLobby(string requestedUasId, string lobbyId)
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, requestedUasId);
            }
            catch (LobbyServiceException e) when (e is { Reason: LobbyExceptionReason.PlayerNotFound })
            {
                // If Player is not found, they have already left the lobbby or have been kicked out. No need to throw here.
            }
        }

        public async Task<QueryResponse> QueryAllLobbies()
        {
            QueryLobbiesOptions querryLobbyOptions = new ()
            {
                Count = MAX_LOBBIES_TO_SHOW,
                Filters = _querryFilters,
                Order = _queryOrders
            };

            return await LobbyService.Instance.QueryLobbiesAsync(querryLobbyOptions);
        }

        public async Task<Lobby> UpdateLobby(string lobbyId, Dictionary<string, DataObject> data, bool shouldLock)
        {
            UpdateLobbyOptions updateLobbyOptions = new()
            {
                Data = data,
                IsLocked = shouldLock
            };

            return await LobbyService.Instance.UpdateLobbyAsync(lobbyId, updateLobbyOptions);
        }

        public async Task<Lobby> UpdatePlayer(string lobbyId, string playerId, Dictionary<string, PlayerDataObject> data, string allocationId, string connectionInfo)
        {
            UpdatePlayerOptions updatePlayerOptions = new()
            {
                Data = data,
                AllocationId = allocationId,
                ConnectionInfo = connectionInfo
            };

            return await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, updatePlayerOptions);
        }

        public async void SendHeartbeatPing(string lobbyId)
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }

        public async Task<ILobbyEvents> SubscribeToLobby(string lobbyId, LobbyEventCallbacks eventCallbacks)
        {
            return await LobbyService.Instance.SubscribeToLobbyEventsAsync(lobbyId, eventCallbacks);
        }
    }
}