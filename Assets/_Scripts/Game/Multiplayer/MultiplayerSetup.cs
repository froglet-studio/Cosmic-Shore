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
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;

namespace CosmicShore.Game
{
    /// <summary>
    /// Minimal, robust session setup with clean leave for both host & clients.
    /// - Do NOT call LeaveSession() from OnClientDisconnect (prevents cycles).
    /// - Centralized teardown in LeaveSession().
    /// - Host: deletes session (simple & predictable).
    /// - Client: leaves session.
    /// - Uses a guard flag so our own leave doesn't re-trigger logic via NGO callbacks.
    /// </summary>
    public class MultiplayerSetup : SingletonNetwork<MultiplayerSetup>
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";

        [SerializeField] private MiniGameDataSO miniGameData;
        [SerializeField] private ScriptableEventNoParam OnActiveSessionEnd;

        public ISession ActiveSession { get; private set; }

        // Guard to avoid double-handling when our own LeaveAsync/DeleteAsync triggers NGO disconnect
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

                if (miniGameData.IsMultiplayerMode)
                {
                    miniGameData.SetupForMultiplayer();
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

        public void Leave() => LeaveSession().Forget();
        
        // --------------------------
        // Session Bootstrapping
        // --------------------------

        private async UniTaskVoid ExecuteMultiplayerSetup()
        {
            var sessions = await QuerySessions();
            if (sessions != null && sessions.Any())
                await JoinSessionAsClientById(sessions.First().Id);
            else
                await StartSessionAsHost();
        }

        private async UniTask StartSessionAsHost()
        {
            var playerProperties = await GetPlayerProperties();

            var sessionOpts = new SessionOptions
            {
                MaxPlayers = miniGameData.SelectedPlayerCount.Value,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties,
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            Debug.Log($"[MultiplayerSetup] Created session {ActiveSession.Id}");
        }

        private async UniTask JoinSessionAsClientById(string sessionId)
        {
            var playerProperties = await GetPlayerProperties();

            var joinOpts = new JoinSessionOptions
            {
                PlayerProperties = playerProperties,
            };

            Debug.Log($"[MultiplayerSetup] Joining session {sessionId}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOpts);
        }

        private async UniTask<IList<ISessionInfo>> QuerySessions()
        {
            var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
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

            // If we're currently performing a self-initiated leave, ignore disconnect noise.
            if (_leaving)
                return;

            // HOST: a client disconnected; keep running.
            if (NetworkManager.Singleton.IsHost)
            {
                if (clientId != NetworkManager.Singleton.LocalClientId)
                {
                    Debug.Log($"[MultiplayerSetup] Client {clientId} disconnected from host.");
                    // Optional: notify gameplay systems/UI here
                }
                return;
            }

            // CLIENT: if the local client got disconnected, host is gone / connection lost → go to menu.
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[MultiplayerSetup] Disconnected from host. Returning to menu.");
                // DO NOT call LeaveSession() here to avoid cycles.
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
                { PLAYER_NAME_PROPERTY_KEY, new PlayerProperty(playerName, VisibilityPropertyOptions.Member) }
            };
        }

        // --------------------------
        // Public Leave Entry Point
        // --------------------------

        /// <summary>
        /// Centralized, safe leave:
        /// - Host: Delete session (simple/consistent).
        /// - Client: Leave session.
        /// - Shutdown NGO once.
        /// - Return to main menu.
        /// This leaves everything clean so the player can create/join again later in this runtime.
        /// </summary>
        async UniTask LeaveSession()
        {
            if (_leaving)
                return;

            _leaving = true;

            try
            {
                if (ActiveSession != null)
                {
                    if (ActiveSession.IsHost)
                    {
                        // End session for everyone; clients will receive disconnect and return to menu.
                        await ActiveSession.AsHost().DeleteAsync();
                        Debug.Log("[MultiplayerSetup] Host deleted session.");
                    }
                    else
                    {
                        await ActiveSession.LeaveAsync();
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
                ActiveSession = null;

                // Local transport cleanup
                if (NetworkManager.Singleton)
                    NetworkManager.Singleton.Shutdown();

                // Back to menu UI/flow
                OnActiveSessionEnd?.Raise();

                _leaving = false;
            }
        }
    }
}
