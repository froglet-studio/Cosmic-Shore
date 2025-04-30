using CosmicShore.Integrations;
using CosmicShore.NetworkManagement;
using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;

namespace CosmicShore.Game.UI
{
    public class MultiplayerUIMediator : NetworkBehaviour
    {
        const string k_DefaultLobbyName = "New_Lobby";

        [Header("UI References")]
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] GameObject _loadingSpinner;
        [SerializeField] Button _onePlayerBtn;
        [SerializeField] Button _twoPlayerBtn;
        [SerializeField] Button _threePlayerBtn;
        [SerializeField] Button _playBtn;

        // injected services
        UnityAuthenticationServiceFacade _auth;
        LobbyServiceFacade _lobbyFacade;
        LocalLobbyUser _localUser;
        LocalLobby _localLobby;
        ConnectionManager _connectionManager;
        NameGenerationData _nameGen;
        ISubscriber<ConnectStatus> _statusSub;

        int _selectedMaxPlayers = 3;
        float _heartbeatInterval = 15f;
        float _heartbeatTimer = 0f;
        bool _isLobbyHost = false;

        [Inject]
        void Inject(
            UnityAuthenticationServiceFacade auth,
            LobbyServiceFacade lobbyFacade,
            LocalLobbyUser localUser,
            LocalLobby localLobby,
            NameGenerationData nameGen,
            ISubscriber<ConnectStatus> statusSub,
            ConnectionManager connMgr
        )
        {
            _auth = auth;
            _lobbyFacade = lobbyFacade;
            _localUser = localUser;
            _localLobby = localLobby;
            _connectionManager = connMgr;
            _nameGen = nameGen;
            _statusSub = statusSub;

            RegenerateName();
            _statusSub.Subscribe(OnConnectStatus);
        }

        void Start()
        {
            _onePlayerBtn.onClick.AddListener(() => SelectPlayerCount(1));
            _twoPlayerBtn.onClick.AddListener(() => SelectPlayerCount(2));
            _threePlayerBtn.onClick.AddListener(() => SelectPlayerCount(3));
            _playBtn.onClick.AddListener(JoinOrCreateLobby);

            SelectPlayerCount(1);
        }

        void Update()
        {
            if (_isLobbyHost)
            {
                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= _heartbeatInterval)
                {
                    _heartbeatTimer = 0f;
                    _ = SendLobbyHeartbeat();
                }
            }
        }

        async System.Threading.Tasks.Task SendLobbyHeartbeat()
        {
            if (_localLobby != null && !string.IsNullOrEmpty(_localLobby.LobbyID))
            {
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_localLobby.LobbyID);
                    Debug.Log("Heartbeat sent to lobby.");
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"Failed to send heartbeat: {e}");
                }
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _statusSub?.Unsubscribe(OnConnectStatus);
        }

        void SelectPlayerCount(int n)
        {
            _selectedMaxPlayers = n;
            Debug.Log($"Selected room size: {n}");
        }

        internal async void JoinOrCreateLobby()
        {
            BlockUI();

            Debug.Log("JoinOrCreateLobby: Ensuring player is authorized...");
            if (!await _auth.EnsurePlayerIsAuthorized())
            {
                Debug.LogWarning("JoinOrCreateLobby: Authorization failed – aborting.");
                UnblockUI();
                return;
            }

            Debug.Log("JoinOrCreateLobby: Attempting TryQuickJoinLobbyAsync...");
            var quickResult = await _lobbyFacade.TryQuickJoinLobbyAsync();
            if (quickResult.Success)
            {
                Debug.Log($"JoinOrCreateLobby: Quick-join succeeded ? Id={quickResult.Lobby.Id}, Code={quickResult.Lobby.LobbyCode}");
                OnJoinedLobby(quickResult.Lobby);
                return;
            }
            Debug.Log($"JoinOrCreateLobby: Quick-join failed. Proceeding to manual query.");

            List<Lobby> availableLobbies = null;
            try
            {
                Debug.Log("JoinOrCreateLobby: Querying up to 20 lobbies...");
                var queryOptions = new QueryLobbiesOptions { Count = 20 };
                var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                availableLobbies = queryResponse.Results.ToList();

                Debug.Log($"JoinOrCreateLobby: Found {availableLobbies.Count} open lobbies:");
                foreach (var lob in availableLobbies)
                {
                    Debug.Log($"  • Id={lob.Id}  Players={lob.Players.Count}/{lob.MaxPlayers}");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"JoinOrCreateLobby: Query failed: {e.Reason} ({e.ErrorCode}): {e.Message}");
            }

            if (availableLobbies != null && availableLobbies.Count > 0)
            {
                var first = availableLobbies[0];
                Debug.Log($"Joining by ID ? Id={first.Id}");
                try
                {
                    Lobby joined = await LobbyService.Instance.JoinLobbyByIdAsync(first.Id);
                    Debug.Log($"JoinLobbyByIdAsync succeeded ? Id={joined.Id}, Code={joined.LobbyCode}");
                    OnJoinedLobby(joined);
                    return;
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"Join by ID failed: {e.Reason} ({e.ErrorCode})");
                }
            }

            Debug.Log("JoinOrCreateLobby: No joinable lobby found – creating a new lobby now.");
            CreateLobbyRequest();
        }

        async void CreateLobbyRequest(bool isPrivate = false, string lobbyName = null)
        {
            if (string.IsNullOrWhiteSpace(lobbyName))
                lobbyName = k_DefaultLobbyName;

            if (!await _auth.EnsurePlayerIsAuthorized())
            {
                UnblockUI();
                return;
            }

            var options = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
        {
            { "S2", new DataObject(DataObject.VisibilityOptions.Public, "cosmic_game_mode") }
        }
            };

            var lobbyCreationAttempt = await _lobbyFacade.TryCreateLobbyAsync(lobbyName, 4, isPrivate, options);

            if (lobbyCreationAttempt.Success)
            {
                Lobby createdLobby = lobbyCreationAttempt.Lobby;

                _localUser.IsHost = true;
                _isLobbyHost = true;

                _lobbyFacade.SetRemoteLobby(createdLobby);

                Debug.Log($"Created lobby with ID: {createdLobby.Id}, Code: {createdLobby.LobbyCode}");

                // ? Pass the created lobby directly
                await SetupRelayAndSaveToLobby(createdLobby);
                _connectionManager.StartHostLobby(_localUser.PlayerName);
            }
            else
            {
                Debug.LogWarning($"Lobby creation failed: {lobbyCreationAttempt}");
                UnblockUI();
            }
        }


        async System.Threading.Tasks.Task SetupRelayAndSaveToLobby(Lobby createdLobby)
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(_selectedMaxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log($"[Host] Relay Join Code: {relayJoinCode}");

                // ? Use lobby ID from the passed lobby (not _localLobby)
                await LobbyService.Instance.UpdateLobbyAsync(createdLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
            {
                { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
            }
                });

                var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Failed to create Relay allocation: {e.Message}");
            }
        }


        void OnJoinedLobby(Lobby remote)
        {
            _lobbyFacade.SetRemoteLobby(remote);
            Debug.Log($"Joined lobby {remote.Id} (Code: {remote.LobbyCode}), starting client...");

            // ?? Client fetches Relay JoinCode
            if (remote.Data.TryGetValue("RelayJoinCode", out var relayJoinCodeData))
            {
                string relayJoinCode = relayJoinCodeData.Value;
                Debug.Log($"[Client] RelayJoinCode found: {relayJoinCode}");
                StartCoroutine(DelayedRelayJoin(relayJoinCode));

            }
            else
            {
                Debug.LogError("RelayJoinCode not found in lobby data!");
            }

            _connectionManager.StartClientLobby(_localUser.PlayerName);
        }

        IEnumerator DelayedRelayJoin(string relayJoinCode)
        {
            Debug.Log("[Client] Waiting briefly before joining Relay...");
            yield return new WaitForSeconds(1.5f); // Delay for Relay backend to sync
            SetupRelayClient(relayJoinCode);
        }


        async void SetupRelayClient(string joinCode)
        {
            int retries = 3;
            while (retries-- > 0)
            {
                try
                {
                    JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                    var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                    transport.SetClientRelayData(
                        joinAllocation.RelayServer.IpV4,
                        (ushort)joinAllocation.RelayServer.Port,
                        joinAllocation.AllocationIdBytes,
                        joinAllocation.Key,
                        joinAllocation.ConnectionData,
                        joinAllocation.HostConnectionData
                    );

                    Debug.Log("[Client] Successfully joined Relay.");
                    return;
                }
                catch (RelayServiceException e)
                {
                    Debug.LogWarning($"Relay join failed: {e.Message} — retries left: {retries}");
                    await System.Threading.Tasks.Task.Delay(1000); // 1 second delay
                }
            }

            Debug.LogError("?? Final attempt to join Relay failed.");
        }


        void RegenerateName()
        {
            _localUser.PlayerName = _nameGen.GenerateName();
        }

        void BlockUI()
        {
            _canvasGroup.interactable = false;
            _loadingSpinner.SetActive(true);
        }

        void UnblockUI()
        {
            _canvasGroup.interactable = true;
            _loadingSpinner.SetActive(false);
        }

        void OnConnectStatus(ConnectStatus status)
        {
            if (status == ConnectStatus.GenericDisconnect || status == ConnectStatus.StartClientFailed)
            {
                Debug.LogWarning($"Connection status error: {status}");
            }
        }
    }
}
