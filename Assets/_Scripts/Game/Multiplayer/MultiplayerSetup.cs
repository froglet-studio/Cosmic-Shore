using System;
using System.Linq;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using System.Collections.Generic;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using CosmicShore.Utility.ClassExtensions;
using Obvious.Soap;

namespace CosmicShore.Game
{
    public class MultiplayerSetup : SingletonNetwork<MultiplayerSetup>
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";

        [SerializeField] MiniGameDataSO miniGameData;
        [SerializeField] ScriptableEventNoParam OnLoadMainMenu;

        ISession _activeSession;
        public ISession ActiveSession { get; private set; }

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

        async void Start()
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
                if (IsHost)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
        }

        async UniTaskVoid ExecuteMultiplayerSetup()
        {
            var sessions = await QuerySessions();
            if (sessions != null && sessions.Any())
                await JoinSessionAsClientById(sessions.First().Id);
            else
                await StartSessionAsHost();
        }

        async UniTask StartSessionAsHost()
        {
            var playerProperties = await GetPlayerProperties();
            var sessionOpts = new SessionOptions
            {
                MaxPlayers = 3, //miniGameData.SelectedPlayerCount.Value,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties,
            }.WithRelayNetwork();

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            Debug.Log($"[MultiplayerSetup] Created session {ActiveSession.Id}");
        }

        async UniTask JoinSessionAsClientById(string sessionId)
        {
            Debug.Log($"[MultiplayerSetup] Joining session {sessionId}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, new JoinSessionOptions());
        }

        async UniTask<IList<ISessionInfo>> QuerySessions()
        {
            var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
            return results.Sessions;
        }

        void OnConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request,
                                          NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.PlayerPrefabHash = null;
        }

        void OnClientDisconnect(ulong clientId)
        {
            if (!NetworkManager.Singleton)
                return;
            
            NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            
            // host always = 0
            if (!NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("[MultiplayerSetup] Host disconnected, returning to menu.");
                OnLoadMainMenu?.Raise();
            }
        }

        async UniTask<Dictionary<string, PlayerProperty>> GetPlayerProperties()
        {
            var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            return new Dictionary<string, PlayerProperty>
            {
                { PLAYER_NAME_PROPERTY_KEY, new PlayerProperty(playerName, VisibilityPropertyOptions.Member) }
            };
        }
    }
}
