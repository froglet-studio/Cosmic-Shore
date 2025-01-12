using CosmicShore.Utilities;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using IStartable = VContainer.Unity.IStartable;

namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// An abstraction layer between the direct calls into the Lobby API and the outcomes you actually want
    /// </summary>
    public class LobbyServiceFacade : IDisposable, IStartable
    {
        private const float HEART_BEAT_PERIOD = 8; // the heartbeat must be rate-limited to 5 calls per 30 seconds, We'll aim for longer in case periods don't allign.
        private const float QUERY_RATE = 1f;
        private const float JOIN_RATE = 3f;
        private const float QUICK_JOIN_RATE = 10f;
        private const float HOST_RATE = 3f;


        [Inject] private LifetimeScope _parentLifetimeScope;
        [Inject] private UpdateRunner _updateRunner;
        [Inject] private LocalLobby _localLobby;
        [Inject] private LocalLobbyUser _localLobbyUser;
        [Inject] private IPublisher<UnityServiceErrorMessage> _unityServiceErrorMessagePublisher;
        [Inject] private IPublisher<LobbyListFetchedMessage> _lobbyListFetchedMessagePublisher;

        private LifetimeScope _serviceLifetimeScope;
        private LobbyAPIInterface _lobbyApiInterface;

        private RateLimitCooldown _rateLimitQuery;
        private RateLimitCooldown _rateLimitJoin;
        private RateLimitCooldown _rateLimitQuickJoin;
        private RateLimitCooldown _rateLimitHost;

        public Lobby CurrentUnityLobby { get; private set; }

        private ILobbyEvents _lobbyEvents;

        private bool _isTracking = false;

        private LobbyEventConnectionState _lobbyEventConnectionState = LobbyEventConnectionState.Unknown;

        public void Start()
        {
            _serviceLifetimeScope = _parentLifetimeScope.CreateChild(
                builder =>
                {
                    builder.Register<LobbyAPIInterface>(Lifetime.Singleton);
                });

            _lobbyApiInterface = _serviceLifetimeScope.Container.Resolve<LobbyAPIInterface>();

            _rateLimitQuery = new RateLimitCooldown(QUERY_RATE);
            _rateLimitJoin = new RateLimitCooldown(JOIN_RATE);
            _rateLimitQuickJoin = new RateLimitCooldown(QUICK_JOIN_RATE);
            _rateLimitHost = new RateLimitCooldown(HOST_RATE);
        }

        public void Dispose()
        {
            EndTracking();
            if (_serviceLifetimeScope != null)
            {
                _serviceLifetimeScope.Dispose();
                _serviceLifetimeScope = null;
            }
        }

        /// <summary>
        /// starts tracking lobby events. 
        /// If the local user is the host, it also initiates sending heartbeat pings to keep the lobby alive
        /// </summary>
        public void BeginTracking()
        {
            if (!_isTracking)
            {
                _isTracking = true;
                SubscribeToJoinedLobbyAsync();

                // Only the host sends heartbeat pings to the service to keep the lobby alive
                if (_localLobbyUser.IsHost)
                {
                    // _heartBeatTime = 0;
                    _updateRunner.Subscribe(DoLobbyHeartbeat, HEART_BEAT_PERIOD);           // 1.5f was the last value, reset it if the new value doesn't work
                }
            }
        }


        /// <summary>
        /// stops tracking lobby events. 
        /// If the local user is the host, it also stops sending heartbeat pings. 
        /// If a lobby is currently active, the host will delete it, while other users will leave it.
        /// </summary>
        public void EndTracking()
        {
            if (_isTracking)
            {
                _isTracking = false;
                UnsubscribeToJoinedLobbyAsync();

                // Only the host sends heartbeats pings to the service to keep the lobby alive
                if (_localLobbyUser.IsHost)
                {
                    _updateRunner.Unsubscribe(DoLobbyHeartbeat);
                }
            }

            if (CurrentUnityLobby != null)
            {
                if (_localLobbyUser.IsHost)
                {
                    DeleteLobbyAsync();
                }
                else
                {
                    LeaveLobbyAsync();
                }
            }
        }


        /// <summary>
        /// an asynchronous method that attempts to create a new lobby with the specified name, maximum number of players, and privacy setting. 
        /// It first checks if the rate limit for creating lobbies has been reached. 
        /// If it has, it logs a warning and returns false. If not, it calls the CreateLobby method on the _lobbyApiInterface object. 
        /// If the lobby creation is successful, it returns true and the created lobby. 
        /// If an exception occurs during the creation of the lobby, it checks if the exception is due to rate limiting. 
        /// If it is, it puts the host on cooldown. If the exception is due to another reason, it publishes the error. 
        /// If the lobby creation is not successful, it returns false and null.
        /// </summary>
        /// <param name="lobbyName">The name of the lobby to be created</param>
        /// <param name="maxPlayers"> Maximum number of players allowed in the lobby</param>
        /// <param name="isPrivate">Is the lobby private</param>
        /// <returns></returns>
        public async Task<(bool Success, Lobby Lobby)> TryCreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate)
        {
            if (!_rateLimitHost.CanCall)
            {
                Debug.LogWarning("Create Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                Lobby lobby = await _lobbyApiInterface.CreateLobby(_localLobbyUser.ID, lobbyName, maxPlayers, isPrivate,
                    _localLobbyUser.GetDataForUnityServices(), null);

                return (true, lobby);
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitHost.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }

            return (false, null);
        }


        /// <summary>
        /// an asynchronous method that attempts to join an existing lobby using either a lobby code or a lobby ID. 
        /// It first checks if the rate limit for joining lobbies has been reached or if both the lobby ID and code are null. 
        /// If either condition is met, it logs a warning and returns false. 
        /// If a lobby code is provided, it attempts to join the lobby using the code. 
        /// If no code is provided, it attempts to join the lobby using the ID. 
        /// If the lobby join is successful, it returns true and the joined lobby. 
        /// If an exception occurs during the join, it checks if the exception is due to rate limiting. 
        /// If it is, it puts the join on cooldown. If the exception is due to another reason, it publishes the error. 
        /// If the lobby join is not successful, it returns false and null.
        /// </summary>
        /// <param name="lobbyId">The ID of the lobby to join</param>
        /// <param name="lobbyCode">The code of the lobby to join</param>
        /// <returns>The success status and the joined lobby</returns>
        public async Task<(bool Success, Lobby Lobby)> TryJoinLobbyAsync(string lobbyId, string lobbyCode)
        {
            if (!_rateLimitJoin.CanCall ||
                (lobbyId == null && lobbyCode == null))
            {
                Debug.LogWarning("Join Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                if (!string.IsNullOrEmpty(lobbyCode))
                {
                    Lobby lobby = await _lobbyApiInterface.JoinLobbyByCode(_localLobbyUser.ID, lobbyCode, _localLobbyUser.GetDataForUnityServices());
                    return (true, lobby);
                }
                else
                {
                    Lobby lobby = await _lobbyApiInterface.JoinLobbyById(_localLobbyUser.ID, lobbyId, _localLobbyUser.GetDataForUnityServices());
                    return (true, lobby);
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitJoin.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }

            return (false, null);
        }


        /// <summary>
        /// an asynchronous method that attempts to quickly join a lobby. 
        /// It first checks if the rate limit for quick joining lobbies has been reached. 
        /// If it has, it logs a warning and returns false. 
        /// If not, it calls the QuickJoinLobby method on the _lobbyApiInterface object. 
        /// If the quick join is successful, it returns true and the joined lobby. 
        /// If an exception occurs during the quick join, it checks if the exception is due to rate limiting. 
        /// If it is, it puts the quick join on cooldown. If the exception is due to another reason, it publishes the error. 
        /// If the quick join is not successful, it returns false and null.
        /// </summary>
        /// <returns>The success status and the joined lobby</returns>
        public async Task<(bool Success, Lobby Lobby)> TryQuickJoinLobbyAsync()
        {
            if (!_rateLimitQuickJoin.CanCall)
            {
                Debug.LogWarning("Quick Join Lobby hit the rate limit.");
                return (false, null);
            }

            try
            {
                Lobby lobby = await _lobbyApiInterface.QuickJoinLobby(_localLobbyUser.ID, _localLobbyUser.GetDataForUnityServices());
                return (true, lobby);
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitQuickJoin.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }
            return (false, null);
        }


        /// <summary>
        /// Sets the local lobby to the provided lobby and updates the current unity lobby with the remote lobby data.
        /// </summary>
        /// <param name="lobby">New lobby to set as the local lobby</param>
        public void SetRemoteLobby(Lobby lobby)
        {
            CurrentUnityLobby = lobby;
            _localLobby.ApplyRemoteData(lobby);
        }


        /// <summary>
        /// an asynchronous method that retrieves a list of all active lobbies and publishes it. 
        /// It first checks if the rate limit for querying lobbies has been reached. 
        /// If it has, it logs an error and returns. 
        /// If not, it calls the QueryAllLobbies method on the _lobbyApiInterface object to get a list of all active lobbies. 
        /// It then publishes this list using the _lobbyListFetchedMessagePublisher object. 
        /// If an exception occurs during the retrieval of the lobby list, it checks if the exception is due to rate limiting. 
        /// If it is, it puts the query on cooldown. 
        /// If the exception is due to another reason, it publishes the error.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task RetrieveAndPublishLobbyListAsync()
        {
            if (!_rateLimitQuery.CanCall)
            {
                Debug.LogError("Retrieve lobby list hit the rate limit. Will try again soon...");
                return;
            }

            try
            {
                QueryResponse response = await _lobbyApiInterface.QueryAllLobbies();
                _lobbyListFetchedMessagePublisher.Publish(new LobbyListFetchedMessage(LocalLobby.CreateLocalLobbies(response)));
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitQuery.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }
        }


        /// <summary>
        /// updates the data of the current lobby and unlocks it. 
        /// It first checks if the rate limit for querying lobbies has been reached. 
        /// If not, it retrieves the local lobby data and merges it with the current lobby data. 
        /// Then, it attempts to update the lobby data on the server. 
        /// If the update is successful, it updates the current lobby with the returned result. 
        /// If a rate limit exception occurs during the update, it puts the query on cooldown. 
        /// If any other exception occurs, it publishes the error.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdateLobbyDataAndUnlockAsync()
        {
            if (!_rateLimitQuery.CanCall)
            {
                return;
            }

            Dictionary<string, DataObject> localData = _localLobby.GetDataForUnityServices();

            Dictionary<string, DataObject> currentData = CurrentUnityLobby.Data;
            if (currentData == null)
            {
                currentData = new Dictionary<string, DataObject>();
            }

            foreach (KeyValuePair<string, DataObject> newData in localData)
            {
                if (currentData.ContainsKey(newData.Key))
                {
                    currentData[newData.Key] = newData.Value;
                }
                else
                {
                    currentData.Add(newData.Key, newData.Value);
                }
            }

            try
            {
                Lobby result = await _lobbyApiInterface.UpdateLobby(CurrentUnityLobby.Id, currentData, shouldLock: false);
                if (result != null)
                {
                    CurrentUnityLobby = result;
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitQuery.PutOnCooldown();
                }
                else
                {
                    PublishError(e);
                }
            }
        }


        /// <summary>
        /// updates the data associated with a player in the current lobby. 
        /// It first checks if the rate limit for querying lobbies has been reached. 
        /// If not, it attempts to update the player data on the server. 
        /// If the update is successful, it updates the current lobby with the returned result. 
        /// If a rate limit exception occurs during the update, it puts the query on cooldown. 
        /// If the lobby is not found and the user is not the host, it assumes the lobby has already been deleted and does not publish the error. 
        /// For any other exceptions, it publishes the error.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdatePlayerDataAsync(string allocationId, string connectionInfo)
        {
            if (!_rateLimitQuery.CanCall)
            {
                return;
            }

            try
            {
                Lobby lobby = await _lobbyApiInterface.UpdatePlayer(CurrentUnityLobby.Id, _localLobbyUser.ID, _localLobbyUser.GetDataForUnityServices(), allocationId, connectionInfo);

                if (lobby != null)
                {
                    CurrentUnityLobby = lobby; // Store the most up-to-date lobby now since we have it, instead of waiting for the next heartbeat.
                }
            }
            catch (LobbyServiceException e)
            {
                if (e.Reason == LobbyExceptionReason.RateLimited)
                {
                    _rateLimitQuery.PutOnCooldown();
                }
                // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                else if (e.Reason != LobbyExceptionReason.LobbyNotFound && !_localLobbyUser.IsHost)
                {
                    PublishError(e);
                }
            }
        }


        /// <summary>
        /// tries to reconnect to a lobby.
        /// If the lobby is not found and the user is not the host, it assumes the lobby has been deleted and doesn't report an error. 
        /// For other exceptions, it reports an error.
        /// If reconnection fails, it returns null.
        /// </summary>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task<Lobby> ReconnectToLobbyAsync()
        {
            try
            {
                return await _lobbyApiInterface.ReconnectToLobby(_localLobby.LobbyID);
            }
            catch (LobbyServiceException e)
            {
                // If Lobby is not found and if we are not the host, it has already been deleted.
                // No need to publish the error here.
                if (e.Reason != LobbyExceptionReason.LobbyNotFound && !_localLobbyUser.IsHost)
                {
                    PublishError(e);
                }
            }

            return null;
        }


        /// <summary>
        /// attempts to remove a player from a lobby. 
        /// If the current user is the host, 
        /// it calls the RemovePlayerFromLobby method on the _lobbyApiInterface object with the provided user ID and the local lobby ID. 
        /// If an exception occurs during this process, it publishes the error. 
        /// If the current user is not the host, it logs an error message stating that only the host can remove players from the lobby.
        /// </summary>
        /// <param name="uasId">The ID of the user to remove from the lobby</param>
        public async void RemovePlayerFromLobbyAsync(string uasId)
        {
            if (_localLobbyUser.IsHost)
            {
                try
                {
                    await _lobbyApiInterface.RemovePlayerFromLobby(uasId, _localLobby.LobbyID);
                }
                catch (LobbyServiceException e)
                {
                    PublishError(e);
                }
            }
            else
            {
                Debug.LogError("Only the host can remove other players from the lobby.");
            }
        }



        /// <summary>
        /// attempts to remove the current user from a lobby. 
        /// If the lobby is not found and the user is not the host, 
        /// it assumes the lobby has been deleted and doesn't report an error. 
        /// For other exceptions, it reports an error. 
        /// After the operation, it resets the lobby regardless of whether the operation was successful or not.
        /// </summary>
        private async void LeaveLobbyAsync()
        {
            string uasId  = AuthenticationService.Instance.PlayerId;
            try
            {
                await _lobbyApiInterface.RemovePlayerFromLobby(uasId, _localLobby.LobbyID);
            }
            catch (LobbyServiceException e)
            {
                // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                if (e.Reason != LobbyExceptionReason.LobbyNotFound && !_localLobbyUser.IsHost)
                {
                    PublishError(e);
                }
            }
            finally
            {
                ResetLobby();
            }
        }


        /// <summary>
        /// resets the current lobby state. 
        /// It sets CurrentUnityLobby to null, resets the state of _localLobbyUser if it's not null, and resets _localLobby with _localLobbyUser if _localLobby is not null. 
        /// </summary>
        private void ResetLobby()
        {
            CurrentUnityLobby = null;
            if (_localLobbyUser != null)
            {
                _localLobbyUser.ResetState();
            }
            if (_localLobby != null)
            {
                _localLobby.Reset(_localLobbyUser);
            }

            // no need to disconnect Netcode, it should already be handled by Netcode's Callback to disconnect
        }


        /// <summary>
        /// asynchronous method in the LobbyServiceFacade class that attempts to delete a lobby if the current user is the host. 
        /// If the deletion operation throws a LobbyServiceException, the method catches the exception and publishes the error. 
        /// Regardless of whether the operation succeeds or fails, the method resets the current lobby state. 
        /// If the current user is not the host, the method logs an error message and does not attempt to delete the lobby.
        /// </summary>
        private async void DeleteLobbyAsync()
        {
            if (_localLobbyUser.IsHost)
            {
                try
                {
                    await _lobbyApiInterface.DeleteLobby(_localLobby.LobbyID);
                }
                catch (LobbyServiceException e)
                {
                    PublishError(e);
                }
                finally
                {
                    ResetLobby();
                }
            }
            else
            {
                Debug.LogError("Only the host can delete a lobby.");
            }
        }


        /// <summary>
        /// This method sends a heartbeat ping to the current lobby at regular intervals defined by HEART_BEAT_PERIOD. 
        /// If the lobby is not found and the user is not the host, it assumes the lobby has been deleted and doesn't report an error. 
        /// For other exceptions, it reports an error.
        /// </summary>
        /// <param name="dt">The time since the last frame</param>
        private void DoLobbyHeartbeat(float dt)
        {
            try
            {
                _lobbyApiInterface.SendHeartbeatPing(CurrentUnityLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                if (e.Reason != LobbyExceptionReason.LobbyNotFound && !_localLobbyUser.IsHost)
                {
                    PublishError(e);
                }
            }
        }


        // As the DoLobbyHeartbeat is already getting called after the HEART_BEAT_PERIOD, hence probably we dont need to recheck the interval inside DoLobbyHeartBeat.
        // Delete the commented method after testing.
        /*private void DoLobbyHeartbeat(float dt)
        {
            _heartBeatTime += dt;
            if (_heartBeatTime >= HEART_BEAT_PERIOD)
            {
                _heartBeatTime -= HEART_BEAT_PERIOD;
                try
                {
                    _lobbyApiInterface.SendHeartbeatPing(CurrentUnityLobby.Id);
                }
                catch (LobbyServiceException e)
                {
                    // If Lobby is not found and if we are not the host, it has already been deleted. No need to publish the error here.
                    if (e.Reason != LobbyExceptionReason.LobbyNotFound && !_localLobbyUser.IsHost)
                    {
                        PublishError(e);
                    }
                }
            }
        }*/


        /// <summary>
        /// This asynchronous method subscribes to events related to the joined lobby. 
        /// It sets up callbacks for when the lobby changes, when the user is kicked from the lobby, 
        /// and when the lobby event connection state changes. 
        /// The callbacks are managed by the Lobby SDK and will be unsubscribed when UnsubscribeAsync is called on the _lobbyEvents object.
        /// </summary>
        private async void SubscribeToJoinedLobbyAsync()
        {
            LobbyEventCallbacks lobbyEventCallbacks = new();
            lobbyEventCallbacks.LobbyChanged += OnLobbyChanges;
            lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobby;
            lobbyEventCallbacks.LobbyEventConnectionStateChanged += OnLobbyEventConnectionStateChanged;
            // The LobbyEventCallbacks object created here will now be managed by the Lobby SDK.
            // The callbacks will be unsubscribed from when we call UnsubscribeAsync on the ILobbyEvents object we receive and store here.
            _lobbyEvents = await _lobbyApiInterface.SubscribeToLobby(_localLobby.LobbyID, lobbyEventCallbacks);
        }


        /// <summary>
        /// This asynchronous method unsubscribes from the events of the joined lobby 
        /// if the lobby events object (_lobbyEvents) is not null and the lobby event connection state is not Unsubscribed. 
        /// In the Unity editor, it catches and logs 
        /// </summary>
        private async void UnsubscribeToJoinedLobbyAsync()
        {
            if (_lobbyEvents != null && _lobbyEventConnectionState != LobbyEventConnectionState.Unsubscribed)
            {
#if UNITY_EDITOR
                try
                {
                    await _lobbyEvents.UnsubscribeAsync();
                }
                catch (WebSocketException e)
                {
                    // This exception occurs in the editor when exiting play mode without first leaving the lobby.
                    // This is because Wire closes the websocket internally when exiting playmode in the editor.
                    Debug.Log(e.Message);
                }
#else
                await _lobbyEvents.UnsubscribeAsync();  
#endif
            }
        }


        /// <summary>
        /// This method handles lobby changes. If the lobby is deleted, it resets the lobby and stops tracking. 
        /// If the lobby is updated, it applies the changes to the current lobby and checks if the host is still in the lobby. 
        /// If the host has left, it publishes an error message and stops tracking.
        /// </summary>
        /// <param name="changes">The changes to the lobby</param>
        private void OnLobbyChanges(ILobbyChanges changes)
        {
            if (changes.LobbyDeleted)
            {
                Debug.Log("Lobby deleted");
                ResetLobby();
                EndTracking();
            }
            else
            {
                Debug.Log("Lobby updated");
                changes.ApplyToLobby(CurrentUnityLobby);
                _localLobby.ApplyRemoteData(CurrentUnityLobby);

                // as client, check if host is still in lobby
                if (!_localLobbyUser.IsHost)
                {
                    foreach (KeyValuePair<string, LocalLobbyUser> pair in _localLobby.LocalLobbyUsers)
                    {
                        if (pair.Value.IsHost)
                        {
                            return;
                        }
                    }

                    _unityServiceErrorMessagePublisher.Publish(new UnityServiceErrorMessage("Host left the lobby", "Disconnecting.", UnityServiceErrorMessage.Service.Lobby));
                    EndTracking();
                    // no need to disconnect Netcode, it should already be handled by Netcode's Callback to disconnect
                }
            }
        }


        /// <summary>
        /// This method handles the event of the user being kicked from the lobby. It resets the lobby and stops tracking.
        /// </summary>
        private void OnKickedFromLobby()
        {
            Debug.Log("Kicked from lobby");
            ResetLobby();
            EndTracking();
        }


        /// <summary>
        /// This method updates the lobby event connection state and logs the new state.
        /// </summary>
        /// <param name="state"></param>
        private void OnLobbyEventConnectionStateChanged(LobbyEventConnectionState state)
        {
            _lobbyEventConnectionState = state;
            Debug.Log($"LobbyEventConnectionState changed to {state}");
        }


        /// <summary>
        /// This method publishes an error message with the details of the provided LobbyServiceException. 
        /// The error message includes the service (Lobby), 
        /// </summary>
        /// <param name="e">The LobbyServiceException to publish</param>
        private void PublishError(LobbyServiceException e)
        {
            string reason = e.InnerException == null ? e.Message : $"{e.Message} ({e.InnerException.Message})"; // Lobby error type, then HTTP error type
            _unityServiceErrorMessagePublisher.Publish(new UnityServiceErrorMessage("Lobby Error", reason, UnityServiceErrorMessage.Service.Lobby, e));
            Debug.LogError(reason);
        }
    }
}