using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using Obvious.Soap;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    public class MultiplayerSetup : Singleton<MultiplayerSetup>
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";
        const string GAME_MODE_PROPERTY_KEY = "gameMode";
        const string MAX_PLAYERS_PROPERTY_KEY = "maxPlayers";

        [FormerlySerializedAs("miniGameData")] [SerializeField] private GameDataSO gameData;
        [SerializeField] private ScriptableEventNoParam OnActiveSessionEnd;

        private bool _leaving;

        private void OnEnable()
        {
            if (!NetworkManager.Singleton)
            {
                Debug.LogError("[MultiplayerSetup] NetworkManager missing in scene!");
                return;
            }

            NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApprovalCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }

        private async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (gameData.IsMultiplayerMode)
                {
                    gameData.SetupForMultiplayer();
                    ExecuteMultiplayerSetup().Forget();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerSetup] UGS init/sign-in failed: {ex}");
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            }
        }

        // --------------------------
        // Session Bootstrapping
        // --------------------------

        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
            var sessions = await QuerySessions();

            if (sessions != null && sessions.Any())
            {
                var matchedSession = sessions.FirstOrDefault();
                if (matchedSession != null)
                    await JoinSessionAsClientById(matchedSession.Id);
                else
                    await StartSessionAsHost();
            }
            else
            {
                await StartSessionAsHost();
            }
        }

        private async UniTask StartSessionAsHost()
        {
            var playerProperties = await GetPlayerProperties();
            var sessionProperties = GetSessionProperties();

            // Add game mode as session metadata
            var sessionOpts = new SessionOptions
            {
                MaxPlayers = gameData.SelectedPlayerCount.Value,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties,
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
            var maxPlayers = gameData.SelectedPlayerCount.Value.ToString();

            var filterOption1 = new FilterOption(FilterField.StringIndex1, gameModeString, FilterOperation.Equal);
            var filterOption2 = new FilterOption(FilterField.StringIndex2, maxPlayers, FilterOperation.Equal);
            var queryOptions = new QuerySessionsOptions();
            queryOptions.FilterOptions.Add(filterOption1);
            queryOptions.FilterOptions.Add(filterOption2);

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
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.PlayerPrefabHash = null;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton == null)
                return;

            if (_leaving)
                return;

            if (NetworkManager.Singleton.IsHost)
            {
                if (clientId != NetworkManager.Singleton.LocalClientId)
                {
                    Debug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                }
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[MultiplayerSetup] Disconnected from host. Returning to menu.");
                OnActiveSessionEnd?.Raise();
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
            string gameMode = gameData.GameMode.ToString();
            string maxPlayers = gameData.SelectedPlayerCount.Value.ToString();
            return new Dictionary<string, SessionProperty>
            {
                { GAME_MODE_PROPERTY_KEY, new SessionProperty(gameMode, VisibilityPropertyOptions.Public, PropertyIndex.String1)},
                { MAX_PLAYERS_PROPERTY_KEY, new SessionProperty(maxPlayers, VisibilityPropertyOptions.Public, PropertyIndex.String2) }
            };
        }

        // --------------------------
        // Public Leave Entry Point
        // --------------------------
        public async UniTask LeaveSession()
        {
            if (_leaving)
                return;

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

                if (NetworkManager.Singleton)
                    NetworkManager.Singleton.Shutdown();

                OnActiveSessionEnd?.Raise();
                _leaving = false;
            }
        }
    }
}
