using CosmicShore.Utilities.Network;
using UnityEngine;
using VContainer;

namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to a connected client.
    /// When being disconnected, transitions to the ClientReconnecting state if no reason is given,
    /// or to the Offline state.
    /// </summary>
    internal class ClientConnectedState : OnlineState
    {
        [Inject]
        protected LobbyServiceFacade _lobbyServiceFacade;

        public override void Enter()
        {
            if (_lobbyServiceFacade.CurrentUnityLobby != null)
            {
                _lobbyServiceFacade.BeginTracking();
            }
        }

        public override void Exit() { }

        public override void OnClientDisconnect(ulong _)
        {
            string disconnectReason = _connectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason))
            {
                _connectStatusPublisher.Publish(ConnectStatus.Reconnecting);
                _connectionManager.ChangeState(_connectionManager._clientReconnectingState);
            }
            else
            {
                ConnectStatus connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                _connectStatusPublisher.Publish(connectStatus);
                _connectionManager.ChangeState(_connectionManager._offlineState);
            }
        }
    }
}