using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using Unity.Netcode;
using UnityEngine;
using VContainer;


namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to a listening host. Handles incoming client connections. 
    /// When shutting down or being timed out, transitions to the Offline state.
    /// </summary>
    internal class HostingState : OnlineState
    {
        private const int MAX_CONNECTED_PAYLOAD = 1024;

        [Inject]
        private SceneNameListSO _sceneNameList;

        [Inject]
        private LobbyServiceFacade _lobbyServiceFacade;

        [Inject]
        private IPublisher<ConnectionEventMessage> _connectionEventPublisher;

        public override void Enter()
        {
            // The "Cosmos" server always advances to CharSelect immediately on Start.
            // Different games may do this differently.
            SceneLoaderWrapper.Instance.LoadScene(_sceneNameList.CharSelectScene, useNetworkSceneManager: true);

            if (_lobbyServiceFacade.CurrentUnityLobby != null)
            {
                _lobbyServiceFacade.BeginTracking();
            }
        }

        public override void Exit()
        {
            SessionManager<SessionPlayerData>.Instance.OnServerEnded();
        }

        public override void OnClientConnected(ulong clientId)
        {
            SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(clientId);
            if (sessionPlayerData != null)
            {
                _connectionEventPublisher.Publish(new ConnectionEventMessage() { ConnectStatus = ConnectStatus.Success, PlayerName = sessionPlayerData.Value.PlayerName });
            }
            else
            {
                // This should not happen since player data is assigned during connection approval.
                Debug.LogError($"No player data associated with client {clientId}");
                string reason = JsonUtility.ToJson(ConnectStatus.GenericDisconnect);
                _connectionManager.NetworkManager.DisconnectClient(clientId, reason);
            }
        }

        public override void OnClientDisconnect(ulong clientId)
        {
            if (clientId != _connectionManager.NetworkManager.LocalClientId)
            {
                string playerId = SessionManager<SessionPlayerData>.Instance.GetPlayerId(clientId);
                if (playerId != null)
                {
                    SessionPlayerData? sessionPlayerData = SessionManager<SessionPlayerData>.Instance.GetPlayerData(playerId);
                    if (sessionPlayerData.HasValue)
                    {
                        _connectionEventPublisher.Publish(new ConnectionEventMessage() { ConnectStatus = ConnectStatus.GenericDisconnect, PlayerName = sessionPlayerData.Value.PlayerName });
                    }
                    SessionManager<SessionPlayerData>.Instance.DisconnectClient(clientId);
                }
            }
        }

        public override void OnUserRequestedShutdown()
        {
            string reason = JsonUtility.ToJson(ConnectStatus.HostEndedSession);
            for (int i = _connectionManager.NetworkManager.ConnectedClientsIds.Count - 1; i >= 0; i--)
            {
                ulong id = _connectionManager.NetworkManager.ConnectedClientsIds[i];
                if (id != _connectionManager.NetworkManager.LocalClientId)
                {
                    _connectionManager.NetworkManager.DisconnectClient(id, reason);
                }
            }
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }

        public override void OnServerStopped()
        {
            _connectStatusPublisher.Publish(ConnectStatus.GenericDisconnect);
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }

        /// <summary>
        /// This logic plugs into the "ConnectionApprovalResponse" exposed by Netcode.NetworkManager. It is run every time a client connects to us.
        /// The complementary logic that runs when the client starts its connection can be found in ClientConnectingState.
        /// </summary>
        /// <remarks>
        /// Multiple things can be done here, some asynchronously. For example, it could authenticate your user against an auth service
        /// like UGS' auth service. It can also send custom messages to connecting users before they receive their connection result
        /// (this is useful to set status message client side when connection is refused. for example).
        /// 
        /// Note on authentication: It's usually harder to justify having authentication in a client hosted game's connection approval.
        /// Since the host can't be trusted, clients shouldn't send it private authentication tokens you'd usually send to a dedicated server.
        /// </remarks>
        /// <param name="request">The initial request contains, among other things, binary data passed into StartClient. In our case, this is the client's GUID,
        /// which is unique indentifier for their install of the game that persists across app restarts.</param>
        /// <param name="response"> Our response to the approval process. In case of connection refusal with custom return message, we delay using the Pending field.</param>
        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            byte[] connectionData = request.Payload;
            ulong clientId = request.ClientNetworkId;
            if (connectionData.Length > MAX_CONNECTED_PAYLOAD)
            {
                // If connectionData too hight, deny immediately to avoid wasting time on the server.
                // This is intended as a bit of light protection against DOS attacks that rely on sending silly big buffers of garbage.
                response.Approved = false;
                return;
            }

            string payload = System.Text.Encoding.UTF8.GetString(connectionData);
            ConnectionPayload connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload); // https://docs.unity3d.com/2020.2/Documentation/Manual/JSONSerialization.html
            ConnectStatus gameReturnStatus = GetConnectStatus(connectionPayload);

            if (gameReturnStatus == ConnectStatus.Success)
            {
                SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(clientId, connectionPayload.playerId,
                    new SessionPlayerData(clientId, connectionPayload.playerName, new NetworkGuid(), true));

                // connection approval will create a player object for you
                response.Approved = true;
                response.CreatePlayerObject = true;
                response.Position = Vector3.zero;
                response.Rotation = Quaternion.identity;
                return;
            }

            response.Approved = false;
            response.Reason = JsonUtility.ToJson(gameReturnStatus);
            if (_lobbyServiceFacade.CurrentUnityLobby != null)
            {
                _lobbyServiceFacade.RemovePlayerFromLobbyAsync(connectionPayload.playerId);
            }
        }

        private ConnectStatus GetConnectStatus(ConnectionPayload connectionPayload)
        {
            if (_connectionManager.NetworkManager.ConnectedClientsIds.Count >= _connectionManager.MaxConnectedPlayers)
            {
                return ConnectStatus.ServerFull;
            }

            if (connectionPayload.isDebug != Debug.isDebugBuild)
            {
                return ConnectStatus.IncompatibleBuildType;
            }

            return SessionManager<SessionPlayerData>.Instance.IsDuplicateConnection(connectionPayload.playerId) ?
                ConnectStatus.LoggedInAgain : ConnectStatus.Success;
        }
    }
}