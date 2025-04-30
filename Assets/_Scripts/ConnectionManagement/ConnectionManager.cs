using CosmicShore.Utilities.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Lobbies.Http;
using UnityEngine;
using VContainer;

namespace CosmicShore.NetworkManagement
{
    public enum ConnectStatus
    {
        Undefined,
        Success,
        ServerFull,
        LoggedInAgain,
        UserRequestedDisconnect,
        KickedByHost,
        GenericDisconnect,
        Reconnecting,
        IncompatibleBuildType,
        HostEndedSession,
        StartHostFailed,
        StartClientFailed,
    }

    public struct ReconnectMessage
    {
        public int CurrentAttempt;
        public int MaxAttempts;

        public ReconnectMessage(int currentAttempt, int maxAttempts)
        {
            CurrentAttempt = currentAttempt;
            MaxAttempts = maxAttempts;
        }
    }

    public struct ConnectionEventMessage : INetworkSerializeByMemcpy
    {
        public ConnectStatus ConnectStatus;
        public FixedPlayerName PlayerName;
    }

    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;
        public string playerName;
        public bool isDebug;
    }

    /// <summary>
    /// ConnectionManager centralizes Netcode and Lobby Service lifecycles,
    /// including heartbeats to keep the lobby alive.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [Inject] NetworkManager _networkManager;
        [Inject] IObjectResolver _objectResolver;
        [Inject] LocalLobby _localLobby;

        [SerializeField] int _reconnectAttempts = 2;
        [SerializeField, Tooltip("Seconds between lobby heartbeat pings")] float _heartbeatInterval = 50f;
        [SerializeField] int _maxConnectedPlayers = 4;

        internal readonly OfflineState _offlineState = new();
        internal readonly ClientConnectingState _clientConnectingState = new();
        internal readonly ClientConnectedState _clientConnectedState = new();
        internal readonly ClientReconnectingState _clientReconnectingState = new();
        internal readonly StartingHostState _startingHostState = new();
        internal readonly HostingState _hostingState = new();

        private ConnectionState _currentState;
        /// <summary>
        /// Provides the current connection state.
        /// </summary>
        internal ConnectionState CurrentState => _currentState;

        private string _currentLobbyId;
        private bool _isHost;
        private Coroutine _heartbeatCoroutine;

        public int ReconnectAttempts => _reconnectAttempts;
        public int MaxConnectedPlayers { get => _maxConnectedPlayers; set => _maxConnectedPlayers = value; }
        public NetworkManager NetworkManager => _networkManager;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Inject states
            var states = new List<ConnectionState>
            {
                _offlineState, _clientConnectingState, _clientConnectedState,
                _clientReconnectingState, _startingHostState, _hostingState
            };
            foreach (var s in states) _objectResolver.Inject(s);

            _currentState = _offlineState;

            // Subscribe callbacks
            _networkManager.OnServerStarted += OnServerStarted;
            _networkManager.OnClientConnectedCallback += OnClientConnectedCallback;
            _networkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
            _networkManager.ConnectionApprovalCallback += ConnectionApprovalCallback;
            _networkManager.OnTransportFailure += OnTransportFailure;
            _networkManager.OnServerStopped += OnServerStopped;
        }

        private void OnDestroy()
        {
            _networkManager.OnServerStarted -= OnServerStarted;
            _networkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            _networkManager.ConnectionApprovalCallback -= ConnectionApprovalCallback;
            _networkManager.OnTransportFailure -= OnTransportFailure;
            _networkManager.OnServerStopped -= OnServerStopped;
        }

        internal void ChangeState(ConnectionState nextState)
        {
            _currentState?.Exit();
            Debug.Log($"[ConnectionManager] State: {_currentState?.GetType().Name} -> {nextState.GetType().Name}");
            _currentState = nextState;
            _currentState.Enter();
        }

        /// <summary>
        /// Starts hosting with lobby heartbeat.
        /// </summary>
        public void StartHostLobby(string playerName)
        {
            _currentLobbyId = _localLobby.LobbyID;
            _isHost = true;
            Debug.Log($"[ConnectionManager] Starting HostLobby for LobbyID={_currentLobbyId}");

            // Immediate ping to verify connectivity
            _ = SendHeartbeat();
            // Start recurring heartbeat
            _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());

            _currentState.StartHostLobby(playerName);
        }

        /// <summary>
        /// Stops hosting and heartbeat.
        /// </summary>
        public void StopHostLobby()
        {
            _isHost = false;
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
                Debug.Log("[ConnectionManager] HeartbeatLoop stopped.");
            }
            _currentLobbyId = null;
        }

        public void StartClientLobby(string playerName) => _currentState.StartClientLobby(playerName);
        public void StartHostIP(string playerName, string ip, int port) => _currentState.StartHostIP(playerName, ip, port);
        public void StartClientIP(string playerName, string ip, int port) => _currentState.StartClientIP(playerName, ip, port);
        public void RequestShutdown() => _currentState.OnUserRequestedShutdown();
        public void OnKickedByHost() => _currentState.OnKickedByHost();

        private void OnServerStarted() => _currentState.OnServerStarted();
        private void OnClientConnectedCallback(ulong id) => _currentState.OnClientConnected(id);
        private void OnClientDisconnectCallback(ulong id) => _currentState.OnClientDisconnect(id);
        private void ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res) => _currentState.ApprovalCheck(req, res);
        private void OnTransportFailure() => _currentState.OnTransportFailure();
        private void OnServerStopped(bool _) { _currentState.OnServerStopped(); StopHostLobby(); }

        /// <summary>
        /// Loop that pings the Lobby Service every interval.
        /// </summary>
        private IEnumerator HeartbeatLoop()
        {
            Debug.Log("[ConnectionManager] HeartbeatLoop started.");
            var wait = new WaitForSeconds(_heartbeatInterval);
            while (_isHost && !string.IsNullOrEmpty(_currentLobbyId))
            {
                yield return wait;
                Debug.Log("[ConnectionManager] HeartbeatLoop tick, sending ping...");
                _ = SendHeartbeat();
            }
            Debug.Log("[ConnectionManager] HeartbeatLoop exiting.");
        }

        /// <summary>
        /// Fire-and-forget ping to lobby service.
        /// </summary>
        async Task SendHeartbeat()
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobbyId);
                Debug.Log($"[ConnectionManager] Lobby heartbeat sent ({_currentLobbyId})");
            }
            catch (LobbyServiceException e)
            {
                // e.ErrorCode comes from RequestFailedException
                // e.Reason tells you the specific lobby error
                if (e.ErrorCode == 429 || e.Reason == LobbyExceptionReason.RateLimited)
                {
                    Debug.LogWarning($"[ConnectionManager] Rate-limited (429): {e.Message}");
                }
                else
                {
                    Debug.LogWarning(
                        $"[ConnectionManager] Heartbeat failed — Reason: {e.Reason}, " +
                        $"Code: {e.ErrorCode} ? {e.Message}"
                    );
                }
            }
            catch (Exception ex)
            {
                // Any other unexpected failure
                Debug.LogError($"[ConnectionManager] Unexpected ping error: {ex}");
            }
        }


        /// <summary>
        /// Prints available lobbies in the Console.
        /// </summary>
        public async Task PrintActiveLobbies(int count = 20)
        {
            try
            {
                var options = new QueryLobbiesOptions { Count = count };
                var response = await LobbyService.Instance.QueryLobbiesAsync(options);
                Debug.Log($"=== Active Lobbies ({response.Results.Count}) ===");
                foreach (var lobby in response.Results)
                    Debug.Log($"Lobby ID={lobby.Id}, Players={lobby.Players.Count}/{lobby.MaxPlayers}");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[ConnectionManager] Failed to query lobbies: {e}");
            }
        }
    }
}
