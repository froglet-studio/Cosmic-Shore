using CosmicShore.Utilities;
using UnityEngine.SceneManagement;
using VContainer;
using Unity.Multiplayer.Samples.Utilities;
using CosmicShore.Utilities.Network;


namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// Connection state corresponding to when the NetworkManager is shut down. From this state we can transition to the 
    /// ClientConnecting state -> if starting as a client, 
    /// StartingHost state -> if starting as a host.
    /// </summary>
    internal class OfflineState : ConnectionState
    {
        [Inject]
        private SceneNameListSO _sceneNameList;

        [Inject]
        private LobbyServiceFacade _lobbyServiceFacade;

        [Inject]
        private LocalLobby _localLobby;

        public override void Enter()
        {
            _lobbyServiceFacade.EndTracking();
            _connectionManager.NetworkManager.Shutdown();
            OnUserRequestedShutdown();
        }

        public override void Exit() { }

        public override void StartHostLobby(string lobbyName)
        {
            ConnectionMethodRelay connectionMethodRelay =
                new(_lobbyServiceFacade, _localLobby, _connectionManager, lobbyName);

            _connectionManager.ChangeState(_connectionManager._startingHostState.Configure(connectionMethodRelay));
        }

        public override void StartClientLobby(string playerName)
        {
            ConnectionMethodRelay connectionMethodRelay =
                new(_lobbyServiceFacade, _localLobby, _connectionManager, playerName);

            _connectionManager._clientReconnectingState.Configure(connectionMethodRelay);
            _connectionManager.ChangeState(_connectionManager._clientConnectingState.Configure(connectionMethodRelay));
        }

        public override void StartHostIP(string playerName, string ipAddress, int port)
        {
            ConnectionMethodIP connectionMethodIP =
                new(ipAddress, (ushort)port, _connectionManager, playerName);

            _connectionManager.ChangeState(_connectionManager._startingHostState.Configure(connectionMethodIP));
        }

        public override void StartClientIP(string playerName, string ipAddress, int port)
        {
            ConnectionMethodIP connectionMethodIP =
                new(ipAddress, (ushort)port, _connectionManager, playerName);

            _connectionManager._clientReconnectingState.Configure(connectionMethodIP);
            _connectionManager.ChangeState(_connectionManager._clientConnectingState.Configure(connectionMethodIP));
        }

        public override void OnUserRequestedShutdown()
        {
            if (_connectionManager.CurrentState != _connectionManager._offlineState)
            {
                _connectionManager.ChangeState(_connectionManager._offlineState);
            }

            if (SceneManager.GetActiveScene().name != _sceneNameList.MainMenuScene)
            {
                SceneLoaderWrapper.Instance.LoadScene(_sceneNameList.MainMenuScene, useNetworkSceneManager: false);
            }
        }
    }
}