using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;



namespace CosmicShore.NetworkManagement
{
    /// <summary>
    /// ConnectionMethod contains all setup needed to setup NGO to be ready to start a connection, either host or client side.
    /// Please override this abstract class to add a new transport or way of connecting.
    /// </summary>
    public abstract class ConnectionMethodBase
    {
        protected const string DTLS_CONNECTION_TYPE = "dtls";

        protected ConnectionManager _connectionManager;
        protected readonly string _playerName;

        public ConnectionMethodBase(ConnectionManager connectionManager, string playerName)
        {
            _connectionManager = connectionManager;
            _playerName = playerName;
        }

        /// <summary>
        /// Setup the host connection prior to starting the NetworkManager
        /// </summary>
        /// <returns></returns>
        public abstract Task SetupHostConnectionAsync();

        /// <summary>
        /// Setup the client connection prior to starting the NetworkManager
        /// </summary>
        /// <returns></returns>
        public abstract Task SetupClientConnectionAsync();

        /// <summary>
        /// Setup the client prior to reconnecting
        /// </summary>
        /// <returns></returns>
        public abstract Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync();

        protected void SetConnectionPayload(string playerId, string playerName)
        {
            string payload = JsonUtility.ToJson(new ConnectionPayload()
            {
                playerId = playerId,
                playerName = playerName,
                isDebug = Debug.isDebugBuild
            });

            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

            _connectionManager.NetworkManager.NetworkConfig.ConnectionData = payloadBytes;
        }

        /// Using authentication, this makes sure your session is assosiated with your account and not your device.
        /// This means you could reconnect from a different device for example.
        /// A playerId is also a bit more permanent than player prefs.
        /// In a browser for example, player prefs can be cleared as easily as cookies.
        /// The forked flow here is for debug purposes and to make UGS optional in Cosmos.
        /// This way we can study the sample without setting up a UGS account.
        /// It's recommended to investigate your own initialization and IsSigned flows to see if you need those checks
        /// on your own and react accordingly.
        /// We offer here the option for offline access for debug purposes, but in your own game you might want to
        /// show an error popup and ask your player to connect to the internet.
        protected string GetPlayerId()
        {
            if (Unity.Services.Core.UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.LogError("Unity Services must be initialized!");
                return null;
            }

            return AuthenticationService.Instance.PlayerId;
        }
    }

    /// <summary>
    /// Simple IP connection setup with UTP
    /// </summary>
    internal class ConnectionMethodIP : ConnectionMethodBase
    {
        private string _ipAddress;
        private ushort _port;

        public ConnectionMethodIP(string ip, ushort port, ConnectionManager connectionManager, string playerName)
            : base(connectionManager, playerName)
        {
            _ipAddress = ip;
            _port = port;
            _connectionManager = connectionManager;
        }

        public override async Task SetupHostConnectionAsync()
        {
            SetConnectionPayload(GetPlayerId(), _playerName);   // Need to set connection payload for host as well, as host is a client too
            UnityTransport utp = (UnityTransport)_connectionManager.NetworkManager.NetworkConfig.NetworkTransport;
            utp.SetConnectionData(_ipAddress, _port);
            await Task.CompletedTask;
        }

        public override async Task SetupClientConnectionAsync()
        {
            SetConnectionPayload(GetPlayerId(), _playerName);
            UnityTransport utp = (UnityTransport)_connectionManager.NetworkManager.NetworkConfig.NetworkTransport;
            utp.SetConnectionData(_ipAddress, _port);
            await Task.CompletedTask;
        }

        public override async Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
        {
            await Task.CompletedTask;
            // Nothing to do here
            return (true, true);
        }
    }


    /// <summary>
    /// UTP's Relay connection setup using the Lobby integration
    /// </summary>
    internal class ConnectionMethodRelay : ConnectionMethodBase
    {
        private LobbyServiceFacade _lobbyServiceFacade;
        private LocalLobby _localLobby;

        public ConnectionMethodRelay(LobbyServiceFacade lobbyServiceFacade, LocalLobby localLobby, ConnectionManager connectionManager, string playerName)
            : base(connectionManager, playerName)
        {
            _connectionManager = connectionManager;
            _lobbyServiceFacade = lobbyServiceFacade;
            _localLobby = localLobby;
        }

        public override async Task SetupHostConnectionAsync()
        {
            Debug.Log("Setting up Unity Relay Host Connection");

            // Set the connection payload for the host
            SetConnectionPayload(GetPlayerId(), _playerName);

            // Create an allocation for the host
            Allocation hostAllocation = await RelayService.Instance.CreateAllocationAsync(_connectionManager.MaxConnectedPlayers);

            // Generate a join code for the lobby
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

            Debug.Log($"Relay Host Info - ConnectionData: {string.Join(",", hostAllocation.ConnectionData)}, " +
                      $"AllocationID: {hostAllocation.AllocationId}, Region: {hostAllocation.Region}");

            // Store the join code in the local lobby for clients to use
            _localLobby.RelayJoinCode = joinCode;

            // Update the lobby with Relay information
            await _lobbyServiceFacade.UpdateLobbyDataAndUnlockAsync();
            await _lobbyServiceFacade.UpdatePlayerDataAsync(hostAllocation.AllocationIdBytes.ToString(), joinCode);

            // Configure UnityTransport with RelayServerData
            UnityTransport utp = (UnityTransport)_connectionManager.NetworkManager.NetworkConfig.NetworkTransport;

            RelayServerData serverData = new RelayServerData(
                hostAllocation.ServerEndpoints[0].Host,
                (ushort)hostAllocation.ServerEndpoints[0].Port,
                hostAllocation.AllocationIdBytes,
                hostAllocation.ConnectionData,
                hostAllocation.ConnectionData, // HostConnectionData for host is the same
                hostAllocation.Key,
                hostAllocation.ServerEndpoints[0].Secure,
                hostAllocation.ServerEndpoints[0].ConnectionType == "ws" || hostAllocation.ServerEndpoints[0].ConnectionType == "wss"
            );

            utp.SetRelayServerData(serverData);

            Debug.Log($"Relay Server Initialized. Join Code: {_localLobby.RelayJoinCode}");
        }


        public override async Task SetupClientConnectionAsync()
        {
            Debug.Log("Setting up Unity Relay Client Connection");

            // Set the connection payload for the client
            SetConnectionPayload(GetPlayerId(), _playerName);

            if (_lobbyServiceFacade.CurrentUnityLobby == null)
            {
                throw new System.Exception("Trying to start relay while the lobby isn't set.");
            }

            Debug.Log($"Joining Relay with Code: {_localLobby.RelayJoinCode}");

            // Join the allocation using the relay join code
            JoinAllocation joinedAllocation = await RelayService.Instance.JoinAllocationAsync(_localLobby.RelayJoinCode);

            Debug.Log($"Relay Client Info - ConnectionData: {string.Join(",", joinedAllocation.ConnectionData)}, " +
                      $"HostConnectionData: {string.Join(",", joinedAllocation.HostConnectionData)}, " +
                      $"AllocationID: {joinedAllocation.AllocationId}");

            // Update the lobby with the client Relay information
            await _lobbyServiceFacade.UpdatePlayerDataAsync(joinedAllocation.AllocationId.ToString(), _localLobby.RelayJoinCode);

            // Configure UnityTransport with RelayServerData
            UnityTransport utp = (UnityTransport)_connectionManager.NetworkManager.NetworkConfig.NetworkTransport;

            RelayServerData serverData = new RelayServerData(
                joinedAllocation.ServerEndpoints[0].Host,
                (ushort)joinedAllocation.ServerEndpoints[0].Port,
                joinedAllocation.AllocationIdBytes,
                joinedAllocation.ConnectionData,
                joinedAllocation.HostConnectionData,
                joinedAllocation.Key,
                joinedAllocation.ServerEndpoints[0].Secure,
                joinedAllocation.ServerEndpoints[0].ConnectionType == "ws" || joinedAllocation.ServerEndpoints[0].ConnectionType == "wss"
            );

            utp.SetRelayServerData(serverData);

            Debug.Log($"Client Relay Initialized. Connected using Join Code: {_localLobby.RelayJoinCode}");
        }


        public override async Task<(bool success, bool shouldTryAgain)> SetupClientReconnectionAsync()
        {
            if (_lobbyServiceFacade.CurrentUnityLobby == null)
            {
                Debug.Log("Lobby no longer exists. Stopping reconnection attempts.");
                return (false, false);
            }

            Debug.Log("Attempting to reconnect to the lobby...");

            // Reconnect to the lobby and check if the lobby still exists
            Lobby lobby = await _lobbyServiceFacade.ReconnectToLobbyAsync();
            bool success = lobby != null;

            Debug.Log(success ? "Reconnected to the lobby successfully." : "Failed to reconnect to the lobby.");
            return (success, true);
        }
    }

}