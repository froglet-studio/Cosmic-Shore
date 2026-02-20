using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Reflex.Attributes;

namespace CosmicShore.Systems
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
                Debug.LogError("[MultiplayerSetup] NetworkManager missing in scene!");
            }
        }

        private void OnEnable()
        {
            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;
            networkManager.ConnectionApprovalCallback += OnConnectionApprovalCallback;
            networkManager.OnClientDisconnectCallback += OnClientDisconnect;
            networkManager.OnTransportFailure         += OnTransportFailure;
        }

        private void OnDisable()
        {
            authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;
            networkManager.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            networkManager.OnTransportFailure         -= OnTransportFailure;
        }

        // --------------------------
        // Session Bootstrapping
        // --------------------------

        void OnAuthenticationSignedIn()
        {
            try
            {
                gameData.DestroyPlayerAndVessel();
                ExecuteMultiplayerSetup().Forget();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerSetup] UGS init/sign-in failed: {ex}");
            }
        }
        
        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
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
                    Debug.LogWarning($"[MultiplayerSetup] Join failed for {s.Id}: {sx.Message} — trying next.");
                    continue;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MultiplayerSetup] Unexpected join error for {s.Id}: {ex.Message} — trying next.");
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

            gameData.ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            gameData.InvokeSessionStarted();

            Debug.Log($"[MultiplayerSetup] Created session {gameData.ActiveSession.Id} with GameMode = {gameData.GameMode}");
        }

        private async UniTask JoinSessionAsClientById(string sessionId)
        {
            var playerProperties = await GetPlayerProperties();

            var joinOpts = new JoinSessionOptions
            {
                PlayerProperties = playerProperties
            };

            Debug.Log($"[MultiplayerSetup] Joining session {sessionId}");
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
            Debug.Log($"[MultiplayerSetup] Queried {results.Sessions.Count} sessions for GameMode {gameModeString}");
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
                    Debug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                }
                return;
            }

            if (clientId == networkManager.LocalClientId)
            {
                Debug.Log("[MultiplayerSetup] Disconnected from host. Returning to menu.");
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
                        Debug.Log("[MultiplayerSetup] Host deleted session.");
                    }
                    else
                    {
                        await gameData.ActiveSession.LeaveAsync();
                        Debug.Log("[MultiplayerSetup] Client left session.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MultiplayerSetup] LeaveSession error: {e.Message}");
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
                Debug.LogWarning("[Net] Transport failure. Recreating session/join…");
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
                Debug.LogError($"[Net] Transport failure handling error: {e}");
            }
        }
    }
}
