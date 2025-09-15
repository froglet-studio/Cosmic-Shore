using System;
using System.Linq;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using CosmicShore.Utilities;
using CosmicShore.Game;
using CosmicShore.Game.Arcade;
using CosmicShore.SOAP;
#if !LINUX_BUILD
using Mono.Cecil;
#endif
using CosmicShore.Utility.ClassExtensions;


namespace CosmicShore.Game
{
    public class MultiplayerSetup : SingletonNetworkPersistent<MultiplayerSetup>
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";

        // [SerializeField] ArcadeEventChannelSO OnArcadeMultiplayerModeSelected;
        // [SerializeField] ScriptableEventArcadeData OnArcadeMultiplayerModeSelected;
        
        [SerializeField]
        MiniGameDataSO miniGameData;

        string _multiplayerSceneName;
        int _maxPlayerPerSession;

        ISession _activeSession;
        internal ISession ActiveSession
        {
            get
            {
                if (_activeSession != null) 
                    return _activeSession;
                
                Debug.LogError("[MultiplayerSetup] No active session found");
                return null;
            }

            private set
            {
                _activeSession = value;
                Debug.Log(_activeSession != null
                    ? $"[MultiplayerSetup] Active session set to {_activeSession.Id}"
                    : "[MultiplayerSetup] Active session cleared");
            }
        }


        private void OnEnable()
        {
            DebugExtensions.LogColored("Hi this is multiplayer setuP!", Color.green);

            if (!NetworkManager.Singleton)
            {
                Debug.LogError("[MultiplayerSetup] NetworkManager is not initialized. Ensure it is set up in the scene.");
                return;
            }

            NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApprovalCallback;
            // NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            
            miniGameData.OnLaunchGame += OnLaunchGame;
        }

        async void Start()
        {
            try
            {
                // 1) Initialize UGS and sign-in
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (AuthenticationException ex)
            {
                Debug.LogError($"UGS sign-in failed ({ex.ErrorCode}): {ex.Message}");
                Debug.LogException(ex); // stack trace
                // TODO: fall back to offline mode / disable online-only features
            }
            catch (RequestFailedException ex)
            {
                Debug.LogError($"UGS init/request failed ({ex.ErrorCode}): {ex.Message}");
                Debug.LogException(ex);
                // TODO: show retry UI, backoff, etc.
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error during UGS init/sign-in: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
                // NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }
            
            miniGameData.OnLaunchGame -= OnLaunchGame;
        }

        private void OnLaunchGame()
        {
            if (!miniGameData.IsMultiplayerMode)
                return;
            
            miniGameData.SetupForMultiplayer();
            
            ExecuteMultiplayerSetup(miniGameData.SceneName, miniGameData.SelectedPlayerCount.Value);
        }

        async void ExecuteMultiplayerSetup(string multiplayerSceneName, int maxPlayersPerSession)
        {
            try
            {
                _multiplayerSceneName = multiplayerSceneName;
                _maxPlayerPerSession = maxPlayersPerSession;
                    
                var sessions = await QuerrySessions();

                if (sessions != null && sessions.Any())
                {
                    await JoinSessionAsClientById(sessions.First().Id);
                }
                else
                {
                    Debug.Log("[MultiplayerSetup] No session found, creating new session");
                    await StartSessionAsHost();
                }

                Debug.Log("[MultiplayerSetup] Quick Join complete!");
            }
            catch (SessionException ex)
            {
                Debug.LogError($"[MultiplayerSetup] Session error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerSetup] Unexpected error: {ex}");
            }
        }
        
        void OnConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Debug.Log($"[MultiplayerSetup] Connection approval request from client {request.ClientNetworkId}");

            // Add any additional validation as needed
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = Vector3.zero;
            response.Rotation = Quaternion.identity;
            response.PlayerPrefabHash = null; // Use the default player prefab
        }

        async UniTask StartSessionAsHost()
        {
            var playerProperties = await GetPlayerProperties();
            var sessionOpts = new SessionOptions
            {
                MaxPlayers = _maxPlayerPerSession,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties,
            }.WithRelayNetwork();   // or WithDistributedAuthorityNetwork() to use Distributed Authority instead of Relay

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            Debug.Log($"[MultiplayerSetup] Created session {ActiveSession.Id}, Join Code: {ActiveSession.Code}");
            
            NetworkManager.Singleton.SceneManager.LoadScene(_multiplayerSceneName, LoadSceneMode.Single);
        }

        async UniTask JoinSessionAsClientById(string sessionId)
        {
            Debug.Log($"[MultiplayerSetup] Joining session {sessionId}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                sessionId,
                new JoinSessionOptions()
            );
        }


        async UniTask JoinSessionAsClientByCode(string sessionCode)
        {
            Debug.Log($"[MultiplayerSetup] Joining session {sessionCode}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                sessionCode,
                new JoinSessionOptions()
            );
        }

        async UniTask<IList<ISessionInfo>> QuerrySessions()
        {
            var sessionQueryOptions = new QuerySessionsOptions();
            var results = await MultiplayerService.Instance.QuerySessionsAsync(sessionQueryOptions);
            return results.Sessions;
        }

        async UniTask LeaveSession()
        {
            if (ActiveSession == null)
            {
                Debug.LogError("[MultiplayerSetup] No active session to leave");
                return;
            }

            try
            { 
                await ActiveSession.LeaveAsync();
            }
            catch
            {
                // Ignored as we are exiting the game
            }
            finally
            {
                Debug.Log($"[MultiplayerSetup] Left session {ActiveSession.Id}");
                ActiveSession = null;
            }
        }

        async UniTask KickPlayer(string playerId)
        {
            if (ActiveSession == null)
            {
                Debug.LogError("[MultiplayerSetup] No active session to kick player from");
                return;
            }

            if (!ActiveSession.IsHost)
            {
                Debug.LogError("[MultiplayerSetup] Only the host can kick players");
                return;
            }

            await ActiveSession.AsHost().RemovePlayerAsync(playerId);
            Debug.Log($"[MultiplayerSetup] Kicked player {playerId} from session {ActiveSession.Id}");
        }

        async UniTask<Dictionary<string, PlayerProperty>> GetPlayerProperties() 
        {
            // Custom game-specific properties that apply to an individual player, ie: name, role, skill level, etc.
            var playerName = await AuthenticationService.Instance.GetPlayerNameAsync();
            var playerNameProperty = new PlayerProperty(playerName, VisibilityPropertyOptions.Member);
            return new Dictionary<string, PlayerProperty> { { PLAYER_NAME_PROPERTY_KEY, playerNameProperty } };
        }

        /*void OnClientConnected(ulong clientId)
        {
            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerNetObj != null)
            {
                Player player = playerNetObj.GetComponent<Player>();
                if (player != null)
                {
                    if (player.IsOwner)
                    {
                        player.NetDefaultShipType.Value = miniGameData.SelectedShipClass.Value;
                    }

                    if (IsServer)
                    {
                        player.NetTeam.Value = TeamAssigner.AssignRandomTeam(_assigned);
                    }
                }
            }
        }*/
    }
}

