using CosmicShore.Utilities.Network;
using System;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to a host starting up. Starts the host when entering the state.
    /// If successful, transitions to the Hosting state. If not, transitions back to the Offline state.
    /// </summary>
    internal class StartingHostState : OnlineState
    {
        private ConnectionMethodBase _connectionMethodBase;

        public override void Enter()
        {
            StartHost();
        }


        public override void Exit() { }

        internal StartingHostState Configure(ConnectionMethodBase connectionMethodBase)
        {
            _connectionMethodBase = connectionMethodBase;
            return this;
        }

        public override void OnServerStarted()
        {
            _connectStatusPublisher.Publish(ConnectStatus.Success);
            _connectionManager.ChangeState(_connectionManager._hostingState);
        }

        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            byte[] connectionData = request.Payload;
            ulong clientId = request.ClientNetworkId;

            // This happens when starting as a host, before the end of the StartHost call. In that case, we simply approve ourselves.
            if (clientId == _connectionManager.NetworkManager.LocalClientId)
            {
                string payload = System.Text.Encoding.UTF8.GetString(connectionData);
                ConnectionPayload connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload); // https://docs.unity3d.com/2020.2/Documentation/Manual/JSONSerialization.html

                SessionManager<SessionPlayerData>.Instance.SetupConnectingPlayerSessionData(clientId, connectionPayload.playerId,
                    new SessionPlayerData(clientId, connectionPayload.playerName, new NetworkGuid(), true));

                // connection approval will create a player object for you
                response.Approved = true;
                response.CreatePlayerObject = true;
            }
        }

        public override void OnServerStopped()
        {
            StartHostFailed();
        }

        private async void StartHost()
        {
            try
            {
                await _connectionMethodBase.SetupHostConnectionAsync();

                // NGO's StartHost launches everything
                if (!_connectionManager.NetworkManager.StartHost())
                {
                    StartHostFailed();
                }
            }
            catch (Exception)
            {
                StartHostFailed();
                throw;
            }
        }

        private void StartHostFailed()
        {
            _connectStatusPublisher.Publish(ConnectStatus.StartHostFailed);
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }
    }
}