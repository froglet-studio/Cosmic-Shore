using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using CosmicShore.Utility;
using Reflex.Attributes;
using CosmicShore.ScriptableObjects;
#if UNITY_EDITOR
using Unity.Multiplayer.Playmode;
using Unity.Netcode.Transports.UTP;
#endif
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

        private NetworkManager networkManager;
        private bool _hostStartInProgress;

        private const int RATE_LIMIT_MAX_RETRIES = 3;
        private const int RATE_LIMIT_BASE_DELAY_MS = 2000;

        private static bool IsRateLimitException(Exception e)
        {
            return e.Message != null && e.Message.Contains("Too Many Requests");
        }

        private void Start()
        {
            if (authenticationDataVariable == null)
            {
                CSDebug.LogError("[MultiplayerSetup] authenticationDataVariable was not injected - check AppManager DI registration.");
                return;
            }

            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;

            // If already authenticated (e.g. Bootstrap auth completed before Start),
            // start the host immediately. An OFFLINE session never signs in, so the
            // offline flag is an alternate entry into the same flow - the offline gate
            // inside OnAuthenticationSignedIn takes it from there.
            if (authenticationData.IsSignedIn || gameData.IsOfflineSession)
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
                UnhookJoinTrace(networkManager);
            }
        }

        // --------------------------
        // Join trace
        // --------------------------

        // A join that fails at "Netcode client never connected" has exactly two silent halves:
        // the host's approval + synchronize send, and the client's synchronize + scene load.
        // Neither side logged either, so a failed join produced nothing but the bounce. These
        // hooks log the connection and scene-event milestones on BOTH sides - a handful of lines
        // per join, never per frame - so the next failing log names the half that stalled.
        NetworkSceneManager _tracedSceneManager;

        void HookJoinTrace(NetworkManager nm)
        {
            nm.OnClientConnectedCallback += OnClientConnectedTrace;
            nm.OnServerStarted           += OnNetworkStartedTrace;
            nm.OnClientStarted           += OnNetworkStartedTrace;
            nm.OnServerStopped           += OnNetworkStoppedTrace;
            nm.OnClientStopped           += OnNetworkStoppedTrace;
        }

        void UnhookJoinTrace(NetworkManager nm)
        {
            nm.OnClientConnectedCallback -= OnClientConnectedTrace;
            nm.OnServerStarted           -= OnNetworkStartedTrace;
            nm.OnClientStarted           -= OnNetworkStartedTrace;
            nm.OnServerStopped           -= OnNetworkStoppedTrace;
            nm.OnClientStopped           -= OnNetworkStoppedTrace;
            if (_tracedSceneManager != null)
            {
                _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
                _tracedSceneManager = null;
            }
        }

        void OnNetworkStartedTrace()
        {
            var nm = networkManager;
            if (nm == null) return;
            // The scene manager is rebuilt on every Start*, so re-hook per start.
            var sm = nm.SceneManager;
            if (sm != null && !ReferenceEquals(sm, _tracedSceneManager))
            {
                if (_tracedSceneManager != null) _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
                _tracedSceneManager = sm;
                sm.OnSceneEvent += OnSceneEventTrace;
            }
            CSDebug.Log($"[NetTrace] Network started - IsHost={nm.IsHost} IsServer={nm.IsServer} IsClient={nm.IsClient} " +
                        $"activeScene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                        $"sceneCount={UnityEngine.SceneManagement.SceneManager.sceneCount}");
        }

        void OnNetworkStoppedTrace(bool wasHost)
        {
            CSDebug.Log($"[NetTrace] Network stopped (wasHost={wasHost}).");
            if (_tracedSceneManager != null)
            {
                _tracedSceneManager.OnSceneEvent -= OnSceneEventTrace;
                _tracedSceneManager = null;
            }
        }

        void OnClientConnectedTrace(ulong clientId)
        {
            var nm = networkManager;
            if (nm == null) return;
            string peers = nm.IsServer ? $" connected={nm.ConnectedClientsIds.Count}" : string.Empty;
            CSDebug.Log($"[NetTrace] Client {clientId} connected (synchronized) - seen by {(nm.IsServer ? "server" : "client")}{peers}.");
        }

        void OnSceneEventTrace(SceneEvent e)
        {
            string done = e.ClientsThatCompleted != null ? $" completed={e.ClientsThatCompleted.Count}" : string.Empty;
            string late = e.ClientsThatTimedOut != null && e.ClientsThatTimedOut.Count > 0 ? $" timedOut={e.ClientsThatTimedOut.Count}" : string.Empty;
            CSDebug.Log($"[NetTrace] SceneEvent {e.SceneEventType} scene='{e.SceneName}' mode={e.LoadSceneMode} client={e.ClientId}{done}{late}");
        }

        // --------------------------
        // Session Bootstrapping
        // --------------------------

        // Synchronous by design: nothing here is awaited. ExecuteMultiplayerSetup is the only
        // async work and it is explicitly fire-and-forget, so wrapping this in an async
        // UniTaskVoid added a state machine without changing when any of it ran.
        void OnAuthenticationSignedIn()
        {
            // OFFLINE session (Steam offline mode - see OfflineModeService): the local
            // loopback host IS the session. Never shut it down for matchmaking, and never
            // touch UGS. Wire the Netcode callbacks (idempotent - the scene-placed copy in
            // each game scene needs them too) and, in a game scene, raise SessionStarted so
            // the app state machine reaches InGame exactly as it does online.
            if (gameData.IsOfflineSession)
            {
                EnsureNetcodeCallbacksWired();
                if (gameData.IsMultiplayerMode)
                    gameData.InvokeSessionStarted();
                return;
            }

            EnsureHostStarted();

            if (gameData.IsMultiplayerMode)
            {
                // DestroyPlayerAndVessel() was removed here because it races with
                // ServerPlayerVesselInitializerWithAI.SpawnAIs(). Both run during
                // scene Start(): SpawnAIs() adds AI to gameData.Players, then this
                // method destroys them. Scene-transition cleanup already happens via
                // SceneLoader.LoadSceneAsync() → ResetRuntimeData() + destroyWithScene.
                ExecuteMultiplayerSetup().Forget();
            }
        }

        /// <summary>
        /// Ensures the Bootstrap NetworkManager exists and has this component's Netcode
        /// callbacks (connection approval, client disconnect, transport failure) registered.
        /// Idempotent - re-wires only when the NetworkManager instance changed. Public
        /// because the OFFLINE local host (OfflineModeService) needs the same callback set
        /// before StartHost: the NetworkManager prefab ships ConnectionApproval on, and a
        /// host with no approval callback times out its own local client.
        /// </summary>
        /// <returns>False when no NetworkManager exists (logged); true otherwise.</returns>
        public bool EnsureNetcodeCallbacksWired()
        {
            // NetworkManager should already exist from Bootstrap (DontDestroyOnLoad).
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("<color=#FF0000>[FLOW-1] [MultiplayerSetup] NetworkManager.Singleton is NULL!</color>");
                CSDebug.LogError("[MultiplayerSetup] NetworkManager.Singleton is null - it should exist from the Bootstrap scene.");
                return false;
            }

            // Re-cache and wire callbacks if the NetworkManager instance changed.
            if (networkManager != nm)
            {
                if (networkManager != null)
                {
                    networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                    networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                    networkManager.OnTransportFailure         -= OnTransportFailure;
                    UnhookJoinTrace(networkManager);
                }

                networkManager = nm;
                nm.ConnectionApprovalCallback += OnConnectionApprovalCallback;
                nm.OnClientDisconnectCallback += OnClientDisconnect;
                nm.OnTransportFailure         += OnTransportFailure;
                HookJoinTrace(nm);
                // Already listening when wired (the offline host, an editor re-entry): the
                // start callback has fired, so hook the live scene manager by hand.
                if (nm.IsListening) OnNetworkStartedTrace();
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Wired Netcode callbacks to NetworkManager</color>");
            }

            return true;
        }

        /// <summary>
        /// Ensures the NetworkManager has Netcode callbacks registered and
        /// starts the host exactly once. The NetworkManager lives in the
        /// Bootstrap scene as DontDestroyOnLoad and must already exist.
        /// Subsequent calls are no-ops while the host is already listening.
        /// </summary>
        void EnsureHostStarted()
        {
            // Guard against concurrent calls (e.g. OnSignedIn event + IsSignedIn
            // check both firing before the first call completes).
            if (_hostStartInProgress)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] EnsureHostStarted SKIPPED (already in progress)</color>");
                return;
            }
            _hostStartInProgress = true;
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] EnsureHostStarted START</color>");

            try
            {
                if (!EnsureNetcodeCallbacksWired())
                    return;

                var nm = networkManager;

                if (nm.IsListening)
                {
                    CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Network already running (IsListening=true), skipping StartHost</color>");
                    CSDebug.Log("[MultiplayerSetup] Network already running.");
                    return;
                }

#if UNITY_EDITOR
                // MPPM clones run as separate editor processes on the same machine.
                // Each starts its own local host, so they need unique ports to avoid
                // bind conflicts. Relay transport handles actual multiplayer connections.
                if (!CurrentPlayer.IsMainEditor)
                {
                    var transport = nm.GetComponent<UnityTransport>();
                    if (transport != null)
                    {
                        var tags = CurrentPlayer.ReadOnlyTags();
                        var tagKey = tags != null && tags.Length > 0 ? string.Join("-", tags) : "clone";
                        ushort port = (ushort)(7778 + (ushort)(Math.Abs(tagKey.GetHashCode()) % 100));
                        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
                        CSDebug.Log($"[MultiplayerSetup] MPPM clone '{tagKey}' - local host port {port}.");
                    }
                }
#endif

                // Host startup is delegated to HostConnectionService which creates a
                // Relay-backed party session (via CreateSessionAsync + WithRelayNetwork).
                // When Relay is unreachable, AuthenticationSceneController falls back to
                // OfflineModeService, which starts a plain 127.0.0.1 local host instead
                // (Docs/OFFLINE_MODE.md).
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00FFFF>[FLOW-1] [MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.</color>");
                CSDebug.Log("[MultiplayerSetup] Callbacks wired. Waiting for HostConnectionService to start Relay host.");
            }
            finally
            {
                _hostStartInProgress = false;
            }
        }

        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
            // If a party session was already handed off (from the invite/party system),
            // skip shutdown and matchmaking - the Relay transport is already active
            // and both host and client are connected through it.
            if (gameData.ActiveSession != null)
            {
                CSDebug.Log($"[MultiplayerSetup] Using existing party session {gameData.ActiveSession.Id}");
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
                    CSDebug.LogWarning($"[MultiplayerSetup] Join failed for {s.Id}: {sx.Message} - trying next.");
                    if (IsRateLimitException(sx))
                        await UniTask.Delay(RATE_LIMIT_BASE_DELAY_MS);
                    continue;
                }
                catch (Exception ex)
                {
                    CSDebug.LogWarning($"[MultiplayerSetup] Unexpected join error for {s.Id}: {ex.Message} - trying next.");
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

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    gameData.ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
                    break;
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    CSDebug.LogWarning($"[MultiplayerSetup] Rate limited on CreateSession - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
            }

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

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                    CSDebug.Log($"[MultiplayerSetup] Queried {results.Sessions.Count} sessions for GameMode {gameModeString}");
                    return results.Sessions;
                }
                catch (Exception e) when (attempt < RATE_LIMIT_MAX_RETRIES && IsRateLimitException(e))
                {
                    int delay = RATE_LIMIT_BASE_DELAY_MS * (1 << attempt);
                    CSDebug.LogWarning($"[MultiplayerSetup] Rate limited on QuerySessions - retry {attempt + 1}/{RATE_LIMIT_MAX_RETRIES} in {delay}ms");
                    await UniTask.Delay(delay);
                }
            }
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

            if (networkManager.IsHost)
            {
                if (clientId != networkManager.LocalClientId)
                {
                    CSDebug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                    // Netcode backstop for hard drops (client crash) that may beat the
                    // graceful UGS ISession.PlayerLeaving. Only the Netcode clientId is
                    // available here (no UGS PlayerId), so this reconciles the roster;
                    // invite cleanup is handled by the PlayerLeaving handler / poll.
                    HostConnectionService.Instance?.ReconcilePartyMembersNow();
                }
                return;
            }

            if (clientId == networkManager.LocalClientId)
            {
                CSDebug.Log("[MultiplayerSetup] Host left/disconnected - bouncing to solo menu.");
                // Host-loss recovery: re-establish our OWN solo host in Menu_Main (works
                // from the lava-lamp menu AND any game scene). Routed through the proven
                // self-rescue instead of gameData.InvokeOnSessionEnded() →
                // SceneLoader.HandleActiveSessionEnd, whose defer-to-server guard hangs the
                // client when the server is gone. See Docs/PartySystem/BUGS.md B10.
                if (PartyInviteController.Instance != null)
                    PartyInviteController.Instance.HandleHostLossAsync("Host disconnected").Forget();
                else
                    gameData.InvokeOnSessionEnded(); // fallback: legacy path
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
        // Transport Failure Handler
        // --------------------------
        private async void OnTransportFailure()
        {
            try
            {
                CSDebug.LogWarning("[Net] Transport failure - bouncing to solo menu.");

                // Same self-rescue as host-loss: tear down, shut down NM, reload Menu_Main,
                // and recreate our OWN solo host (EnsurePartySessionAsync). The legacy path
                // below shut NM down but never recreated the solo session, leaving a hostless
                // menu. See Docs/PartySystem/BUGS.md B10.
                if (PartyInviteController.Instance != null)
                {
                    await PartyInviteController.Instance.HandleHostLossAsync("Connection lost");
                    return;
                }

                // Fallback (PartyInviteController unavailable): legacy teardown.
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
