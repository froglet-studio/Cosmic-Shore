using CosmicShore.Utilities;
using Unity.Netcode;
using VContainer;

namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Base class representing a connection state.
    /// </summary>
    internal abstract class ConnectionState
    {
        [Inject]
        protected readonly ConnectionManager _connectionManager;

        [Inject]
        protected IPublisher<ConnectStatus> _connectStatusPublisher;

        public abstract void Enter();
        public abstract void Exit();
        public virtual void OnClientConnected(ulong clientId) { }
        public virtual void OnClientDisconnect(ulong clientId) { }
        public virtual void OnServerStarted() { }
        public virtual void StartClientIP(string playerName, string ipAddress, int port) { }
        public virtual void StartClientLobby(string playerName) { }
        public virtual void StartHostIP(string playerName, string ipAddress, int port) { }
        public virtual void StartHostLobby(string playerName) { }
        public virtual void OnUserRequestedShutdown() { }
        public virtual void OnKickedByHost() { }
        public virtual void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) { }
        public virtual void OnTransportFailure() { }
        public virtual void OnServerStopped() { }
    }
}