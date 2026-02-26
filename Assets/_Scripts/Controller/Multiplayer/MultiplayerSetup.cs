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

         void Awake()
        {
            networkManager = NetworkManager.Singleton; // or NetworkManager.Instance if you’ve wrapped it
            if (!networkManager)
            {
                CSDebug.LogError("[MultiplayerSetup] NetworkManager missing in scene!");
            }
        }

        private void Start()
        {
            if (authenticationDataVariable == null)
            {
                CSDebug.LogError("[MultiplayerSetup] authenticationDataVariable was not injected — check AppManager DI registration.");
                return;
            }

            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;

            if (networkManager != null)
            {
                networkManager.ConnectionApprovalCallback += OnConnectionApprovalCallback;
                networkManager.OnClientDisconnectCallback += OnClientDisconnect;
                networkManager.OnTransportFailure         += OnTransportFailure;
            }

            // If already authenticated (e.g. Menu_Main loaded after auth completed),
            // start the host and run any game-scene setup immediately.
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
            EnsureHostStarted();

            if (gameData.IsMultiplayerMode)
            {
                gameData.DestroyPlayerAndVessel();
                ExecuteMultiplayerSetup().Forget();
            }
        }

        /// <summary>
        /// Starts the Netcode host exactly once. Subsequent calls are no-ops
        /// while the host is already listening. Called after authentication
        /// completes — typically in Menu_Main, then skipped in game scenes.
        /// </summary>
        void EnsureHostStarted()
        {
            if (networkManager == null)
            {
                CSDebug.LogError("[MultiplayerSetup] Cannot start host — NetworkManager is null.");
                return;
            }

            if (networkManager.IsListening)
            {
                CSDebug.Log("[MultiplayerSetup] Host already running.");
                return;
            }

            CSDebug.Log("[MultiplayerSetup] Starting as Host.");
            networkManager.StartHost();
        }

        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
            // Shutdown the local host before creating a Relay-based multiplayer session.
            // This is the single intentional transition from local to Relay transport.
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                await UniTask.WaitUntil(() => !networkManager.IsListening);
            }

            // If a party session was already handed off (e.g. from PartyGameLauncher),
            // skip matchmaking and use the existing session directly.
            if (gameData.ActiveSession != null)
            {
                CSDebug.Log($"[MultiplayerSetup] Using existing party session {gameData.ActiveSession.Id}");
                DomainAssigner.Initialize();
                gameData.InvokeSessionStarted();
                return;
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
