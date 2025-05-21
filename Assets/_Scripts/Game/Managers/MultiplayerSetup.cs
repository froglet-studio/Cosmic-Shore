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
using CosmicShore.Game;


namespace CosmicShore.Core
{
    public class MultiplayerSetup : MonoBehaviour
    {
        const string PLAYER_NAME_PROPERTY_KEY = "playerName";
        const string CONNECTION_TYPE = "dtls";

        [SerializeField]
        int _maxPlayers = 4;

        string _multiplayerSceneName;

        ISession _activeSession;
        ISession ActiveSession
        {
            get
            {
                if (_activeSession == null)
                {
                    Debug.LogError("[MultiplayerSetup] No active session found");
                    return null;
                }
                return _activeSession;
            }

            set
            {
                _activeSession = value;
                if (_activeSession != null)
                {
                    Debug.Log($"[MultiplayerSetup] Active session set to {_activeSession.Id}");
                }
                else
                {
                    Debug.Log("[MultiplayerSetup] Active session cleared");
                }
            }
        }

        async void Awake()
        {
            DontDestroyOnLoad(this.gameObject);

            // 1) Initialize UGS and sign-in
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void OnEnable()
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApprovalCallback;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        private void OnDisable()
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApprovalCallback;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }


        public async void ExecuteMultiplayerSetup(string multiplayerSceneName)
        {
            _multiplayerSceneName = multiplayerSceneName;

            try
            {
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
                MaxPlayers = _maxPlayers,
                IsLocked = false,
                IsPrivate = false,
                PlayerProperties = playerProperties,
            }.WithRelayNetwork();   // or WithDistributedAuthorityNetwork() to use Distributed Authority instead of Relay

            ActiveSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOpts);
            Debug.Log($"[MultiplayerSetup] Created session {ActiveSession.Id}, Join Code: {ActiveSession.Code}");

            // var allocation = await RelayService.Instance.CreateAllocationAsync(_maxPlayers);
            // NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CONNECTION_TYPE));

            // Host is responsible for starting the game
            // Debug.Log($"[MultiplayerSetup] Starting host!");
            // NetworkManager.Singleton.StartHost();
        }

        async UniTask JoinSessionAsClientById(string sessionId)
        {
            Debug.Log($"[MultiplayerSetup] Joining session {sessionId}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                sessionId,
                new JoinSessionOptions()
            );

            // var allocation = await RelayService.Instance.JoinAllocationAsync(ActiveSession.Code);
            // NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CONNECTION_TYPE));

            // Client is responsible for starting the game
            // Debug.Log($"[MultiplayerSetup] Starting client!");
            // NetworkManager.Singleton.StartClient();
        }


        async UniTask JoinSessionAsClientByCode(string sessionCode)
        {
            Debug.Log($"[MultiplayerSetup] Joining session {sessionCode}");
            ActiveSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                sessionCode,
                new JoinSessionOptions()
            );

            // var allocation = await RelayService.Instance.JoinAllocationAsync(ActiveSession.Code);
            // NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, CONNECTION_TYPE));

            // Client is responsible for starting the game
            // Debug.Log($"[MultiplayerSetup] Starting client!");
            // NetworkManager.Singleton.StartClient();
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

        void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.ServerClientId)
                return;

            NetworkManager.Singleton.SceneManager.LoadScene(_multiplayerSceneName, LoadSceneMode.Single);
        }
    }
}

