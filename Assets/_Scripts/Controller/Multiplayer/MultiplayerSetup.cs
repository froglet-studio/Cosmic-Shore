using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using CosmicShore.Utility;
using Reflex.Attributes;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Gameplay
{
    public class MultiplayerSetup : MonoBehaviour
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";
        const string GAME_MODE_PROPERTY_KEY   = "gameMode";
        const string MAX_PLAYERS_PROPERTY_KEY = "maxPlayers";

        [Inject] GameDataSO gameData;
        [Inject] AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        private bool _leaving;

        private NetworkManager networkManager;
        private bool _hostStartInProgress;
        private bool _suppressTransportFailure;

        private void Start()
        {
            if (authenticationDataVariable == null)
            {
                CSDebug.LogError("[MultiplayerSetup] authenticationDataVariable was not injected — check AppManager DI registration.");
                return;
            }

            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;

            // If already authenticated (e.g. Bootstrap auth completed before Start),
            // start the host immediately.
            if (authenticationData.IsSignedIn)
            {
                OnAuthenticationSignedIn();
            }
        }

        private void OnDisable()
        {
            if (authenticationDataVariable == null) return;

            authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;

            if (networkManager != null)
            {
                networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                networkManager.OnTransportFailure         -= OnTransportFailure;
            }
        }

        // --------------------------
        // Session Bootstrapping
        // --------------------------

        void OnAuthenticationSignedIn()
        {
            OnAuthenticationSignedInAsync().Forget();
        }

        async UniTaskVoid OnAuthenticationSignedInAsync()
        {
            EnsureHostStarted();

            if (gameData.IsMultiplayerMode)
            {
                gameData.DestroyPlayerAndVessel();
                ExecuteMultiplayerSetup().Forget();
            }
        }

        /// <summary>
        /// Ensures the NetworkManager has Netcode callbacks registered and
        /// starts the host exactly once. The NetworkManager lives in the
        /// Bootstrap scene as DontDestroyOnLoad and must already exist.
        /// Subsequent calls are no-ops while the host is already listening.
        /// If the default port is already in use (e.g. another editor instance),
        /// retries on incrementing ports.
        /// </summary>
        void EnsureHostStarted()
        {
            // Guard against concurrent calls (e.g. OnSignedIn event + IsSignedIn
            // check both firing before the first call completes).
            if (_hostStartInProgress) return;
            _hostStartInProgress = true;

            try
            {
                // NetworkManager should already exist from Bootstrap (DontDestroyOnLoad).
                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    CSDebug.LogError("[MultiplayerSetup] NetworkManager.Singleton is null — it should exist from the Bootstrap scene.");
                    return;
                }

                // Re-cache and wire callbacks if the NetworkManager instance changed.
                if (networkManager != nm)
                {
                    if (networkManager != null)
                    {
                        networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                        networkManager.OnTransportFailure         -= OnTransportFailure;
                    }

                    networkManager = nm;
                    nm.ConnectionApprovalCallback += OnConnectionApprovalCallback;
                    nm.OnClientDisconnectCallback += OnClientDisconnect;
                    nm.OnTransportFailure         += OnTransportFailure;
                }

                if (nm.IsListening)
                {
                    CSDebug.Log("[MultiplayerSetup] Host already running.");
                    return;
                }

                var transport = nm.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    CSDebug.LogError("[MultiplayerSetup] UnityTransport not found on NetworkManager.");
                    return;
                }

                ushort basePort = transport.ConnectionData.Port;
                const int maxPortAttempts = 10;

                // Suppress OnTransportFailure during port retry loop so it
                // doesn't shut down the NetworkManager between attempts.
                _suppressTransportFailure = true;
                try
                {
                    for (int i = 0; i < maxPortAttempts; i++)
                    {
                        ushort port = (ushort)(basePort + i);
                        if (i > 0)
                        {
                            transport.SetConnectionData(
                                transport.ConnectionData.Address,
                                port,
                                transport.ConnectionData.ServerListenAddress);
                        }

                        CSDebug.Log($"[MultiplayerSetup] Starting as Host on port {port}...");
                        bool started = nm.StartHost();

                        if (started && nm.IsListening)
                        {
                            CSDebug.Log($"[MultiplayerSetup] Host started on port {port}.");
                            return;
                        }

                        CSDebug.LogWarning($"[MultiplayerSetup] StartHost failed on port {port}. Trying next port...");
                    }

                    CSDebug.LogError($"[MultiplayerSetup] Failed to start host after {maxPortAttempts} port attempts ({basePort}–{(ushort)(basePort + maxPortAttempts - 1)}).");
                }
                finally
                {
                    _suppressTransportFailure = false;
                }
            }
            finally
            {
                _hostStartInProgress = false;
            }
        }

        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
            // If a party session was already handed off (from the invite/party system),
            // skip shutdown and matchmaking — the Relay transport is already active
            // and both host and client are connected through it.
            if (gameData.ActiveSession != null)
            {
                CSDebug.Log($"[MultiplayerSetup] Using existing party session {gameData.ActiveSession.Id}");
                DomainAssigner.Initialize();
                gameData.InvokeSessionStarted();
                return;
            }

            // Shutdown the local host before creating a Relay-based multiplayer session.
            // This is the single intentional transition from local to Relay transport.
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                await UniTask.WaitUntil(() => !networkManager.IsListening);
            }

            // Query sessions for this game mode & player count
            var sessions = await QuerySessions();

            // Filter to sessions that look joinable
            var candidates = sessions?
                .Where(IsJoinableSessionInfo)
                .OrderBy(s => s.Created) // older first; tweak if you like
                .ToList() ?? new List<ISessionInfo>();

            // Try to join the first joinable; if race-filled, keep trying others
            if (candidates.Count > 0 && await TryJoinFirstAvailable(candidates))
                return;

            // Nothing joinable → create a fresh host session
            await StartSessionAsHost();
        }

        // Try join loop that handles race conditions (session fills between query and join)
        private async UniTask<bool> TryJoinFirstAvailable(IList<ISessionInfo> candidates)
        {
            foreach (var s in candidates)
            {
                try
                {
                    await JoinSessionAsClientById(s.Id);
                    return true;
                }
                catch (SessionException sx)
                {
                    // Known cases to skip and try next: full/locked/deleted/etc.
                    CSDebug.LogWarning($"[MultiplayerSetup] Join failed for {s.Id}: {sx.Message} — trying next.");
                    continue;
                }
                catch (Exception ex)
                {
                    CSDebug.LogWarning($"[MultiplayerSetup] Unexpected join error for {s.Id}: {ex.Message} — trying next.");
                    continue;
                }
            }
            return false;
        }

        // Decide if a session is joinable based on info
        private bool IsJoinableSessionInfo(ISessionInfo info)
        {
            if (info == null) return false;

            // Defensive: prefer sessions that are not private/locked and have room
            var hasRoom   = (info.MaxPlayers > 0) && (info.AvailableSlots > 0);
            var notLocked = !info.IsLocked;
            var notPrivate= !info.HasPassword;

            return hasRoom && notLocked && notPrivate;
        }

        private async UniTask StartSessionAsHost()
        {
            // Ensure domain pool is fresh before any players connect.
            DomainAssigner.Initialize();

            var playerProperties  = await GetPlayerProperties();
            var sessionProperties = GetSessionProperties();

            var sessionOpts = new SessionOptions
            {
                MaxPlayers        = gameData.SelectedPlayerCount.Value,
                IsLocked          = false,
                IsPrivate         = false,
                PlayerProperties  = playerProperties,
                SessionProperties = sessionProperties
            }.WithRelayNetwork();

            gameData.ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            gameData.InvokeSessionStarted();

            CSDebug.Log($"[MultiplayerSetup] Created session {gameData.ActiveSession.Id} with GameMode = {gameData.GameMode}");
        }

        private async UniTask JoinSessionAsClientById(string sessionId)
        {
            var playerProperties = await GetPlayerProperties();

            var joinOpts = new JoinSessionOptions
            {
                PlayerProperties = playerProperties
            };

            CSDebug.Log($"[MultiplayerSetup] Joining session {sessionId}");
            gameData.ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOpts);
        }

        // --------------------------
        // Query Sessions (filtered by GameMode)
        // --------------------------
        private async UniTask<IList<ISessionInfo>> QuerySessions()
        {
            var gameModeString = gameData.GameMode.ToString();
            var maxPlayers     = gameData.SelectedPlayerCount.Value.ToString();

            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(new FilterOption(FilterField.StringIndex1, gameModeString, FilterOperation.Equal));
            queryOptions.FilterOptions.Add(new FilterOption(FilterField.StringIndex2, maxPlayers,     FilterOperation.Equal));

            var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
            CSDebug.Log($"[MultiplayerSetup] Queried {results.Sessions.Count} sessions for GameMode {gameModeString}");
            return results.Sessions;
        }

        // --------------------------
        // NGO Connection Hooks
        // --------------------------

        private void OnConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request,
                                                  NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved           = true;
            response.CreatePlayerObject = true;
            response.Position           = Vector3.zero;
            response.Rotation           = Quaternion.identity;
            response.PlayerPrefabHash   = null;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (networkManager == null) return;
            if (_leaving)               return;

            if (networkManager.IsHost)
            {
                if (clientId != networkManager.LocalClientId)
                {
                    CSDebug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                }
                return;
            }

            if (clientId == networkManager.LocalClientId)
            {
                CSDebug.Log("[MultiplayerSetup] Disconnected from host. Returning to menu.");
                gameData.InvokeOnSessionEnded();
            }
        }

        // --------------------------
        // Player Properties
        // --------------------------
        private async UniTask<Dictionary<string, PlayerProperty>> GetPlayerProperties()
        {
            var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();

            return new Dictionary<string, PlayerProperty>
            {
                { PLAYER_NAME_PROPERTY_KEY, new PlayerProperty(playerName, VisibilityPropertyOptions.Member) },
            };
        }

        private Dictionary<string, SessionProperty> GetSessionProperties()
        {
            string gameMode   = gameData.GameMode.ToString();
            string maxPlayers = gameData.SelectedPlayerCount.Value.ToString();
            return new Dictionary<string, SessionProperty>
            {
                { GAME_MODE_PROPERTY_KEY,   new SessionProperty(gameMode,   VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                { MAX_PLAYERS_PROPERTY_KEY, new SessionProperty(maxPlayers, VisibilityPropertyOptions.Public, PropertyIndex.String2) }
            };
        }

        // --------------------------
        // Public Leave Entry Point
        // --------------------------
        public async UniTask LeaveSession()
        {
            if (_leaving) return;

            _leaving = true;

            try
            {
                if (gameData.ActiveSession != null)
                {
                    if (gameData.ActiveSession.IsHost)
                    {
                        await gameData.ActiveSession.AsHost().DeleteAsync();
                        CSDebug.Log("[MultiplayerSetup] Host deleted session.");
                    }
                    else
                    {
                        await gameData.ActiveSession.LeaveAsync();
                        CSDebug.Log("[MultiplayerSetup] Client left session.");
                    }
                }
            }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[MultiplayerSetup] LeaveSession error: {e.Message}");
            }
            finally
            {
                gameData.ActiveSession = null;

                if (networkManager != null)
                    networkManager.Shutdown();

                gameData.InvokeOnSessionEnded();
                _leaving = false;
            }
        }

        // --------------------------
        // Transport Failure Handler
        // --------------------------
        private async void OnTransportFailure()
        {
            // During EnsureHostStarted port-retry loop, suppress so the handler
            // doesn't shut down the NetworkManager between attempts.
            if (_suppressTransportFailure) return;

            try
            {
                CSDebug.LogWarning("[Net] Transport failure. Recreating session/join…");
                if (gameData.ActiveSession != null)
                {
                    if (gameData.ActiveSession.IsHost)
                        await gameData.ActiveSession.AsHost().DeleteAsync();
                    else
                        await gameData.ActiveSession.LeaveAsync();

                    gameData.ActiveSession = null;
                }

                if (networkManager != null)
                    networkManager.Shutdown();

                await UniTask.Delay(500);
                gameData.InvokeOnSessionEnded();
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[Net] Transport failure handling error: {e}");
            }
        }
    }
}
