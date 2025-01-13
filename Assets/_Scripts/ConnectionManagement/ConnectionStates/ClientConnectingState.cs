using System;
using System.Threading.Tasks;
using UnityEngine;


namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to when a client is attempting to connect to a server.
    /// Starts the client when entering. If successful, transitions to the ClientConnected state.
    /// If not, transitions to the Offline state.
    /// </summary>
    internal class ClientConnectingState : OnlineState
    {
        protected ConnectionMethodBase _connectionMethodBase;

        public override void Enter()
        {
#pragma warning disable 4014
            ConnectClientAsync();
#pragma warning restore 4014
        }

        public override void Exit() { }

        public ClientConnectingState Configure(ConnectionMethodBase connectionMethodBase)
        {
            _connectionMethodBase = connectionMethodBase;
            return this;
        }

        public override void OnClientConnected(ulong _)
        {
            _connectStatusPublisher.Publish(ConnectStatus.Success);
            _connectionManager.ChangeState(_connectionManager._clientConnectedState);
        }

        public override void OnClientDisconnect(ulong _)
        {
            // client ID is for sure ours here
            StartingClientFailed();
        }

        internal async Task ConnectClientAsync()
        {
            try
            {
                // Setup NGO with current connection method
                await _connectionMethodBase.SetupClientConnectionAsync();

                // NGO's StartClient launches everything
                if (!_connectionManager.NetworkManager.StartClient())
                {
                    throw new System.Exception("NetworkManager StartClient failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error connecting client, see following exception");
                Debug.LogException(e);
                StartingClientFailed();
                throw;
            }
        }

        private void StartingClientFailed()
        {
            string disconnectReason = _connectionManager.NetworkManager.DisconnectReason;
            if (string.IsNullOrEmpty(disconnectReason))
            {
                _connectStatusPublisher.Publish(ConnectStatus.StartClientFailed);
                Debug.LogError(disconnectReason);
            }
            else
            {
                ConnectStatus connectStatus = JsonUtility.FromJson<ConnectStatus>(disconnectReason);
                _connectStatusPublisher.Publish(connectStatus);
                Debug.LogError(connectStatus);
            }
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }
    }
}