namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Base class representing an online connection state.
    /// </summary>
    internal abstract class OnlineState : ConnectionState
    {
        public override void OnUserRequestedShutdown()
        {
            // This behaviour will be the same for every online state
            _connectStatusPublisher.Publish(ConnectStatus.UserRequestedDisconnect);
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }

        public override void OnKickedByHost()
        {
            // Debug.Log("Kicked by host");
            _connectStatusPublisher.Publish(ConnectStatus.KickedByHost);
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }

        public override void OnTransportFailure()
        {
            // This behaviour will be the same for every online state
            _connectionManager.ChangeState(_connectionManager._offlineState);
        }
    }
}