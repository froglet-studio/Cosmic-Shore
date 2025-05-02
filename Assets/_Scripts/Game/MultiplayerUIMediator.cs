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
using System.Threading.Tasks;
using System;

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
            if (enabled)
            {
                if (_localUser != null && _localUser.IsHost && !string.IsNullOrEmpty(_localLobby.LobbyID))
                {
                    _heartbeatTimer += Time.deltaTime;
                    if (_heartbeatTimer >= _heartbeatInterval)
                    {
                        _heartbeatTimer = 0f;
                        _ = SendLobbyHeartbeat();
                    }
                }
            }
        }

        async Task SendLobbyHeartbeat()
        {
            if (!_isLobbyHost || string.IsNullOrEmpty(_localLobby.LobbyID))
                return;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_localLobby.LobbyID);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"Heartbeat failed ({e.Reason})");
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

        // at the top of your class
        bool _joinOrCreateInProgress = false;


        internal async void JoinOrCreateLobby()
        {
            if (_joinOrCreateInProgress)
            {
                Debug.Log("Join/create already in progress, ignoring click.");
                return;
            }
            _joinOrCreateInProgress = true;
            BlockUI();

            if (!await _auth.EnsurePlayerIsAuthorized())
            {
                Debug.LogWarning("Not authorized – aborting.");
                _joinOrCreateInProgress = false;
                UnblockUI();
                return;
            }

            // just grab *any* lobby (up to 10)
            List<Lobby> all;
            try
            {
                var opts = new QueryLobbiesOptions { Count = 10 };
                var resp = await LobbyService.Instance.QueryLobbiesAsync(opts);
                all = resp.Results.ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Query failed ({e.Message})");
                all = new List<Lobby>();
            }

            if (all.Count > 0)
            {
                try
                {
                    var joined = await LobbyService.Instance.JoinLobbyByIdAsync(all[0].Id);
                    Debug.Log($" Joined existing lobby {joined.Id}");
                    OnJoinedLobby(joined);
                    return;   // ? never create
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError($"Join failed ({e.Reason}) – giving up.");
                    _joinOrCreateInProgress = false;
                    UnblockUI();
                    return;
                }
            }

            // nothing to join ? create one
            await CreateLobbyRequestAsync();
        }

        async Task CreateLobbyRequestAsync(bool isPrivate = false, string lobbyName = null)
        {
            // — guard to never recreate if we're already host or have an ID —
            if (_isLobbyHost || !string.IsNullOrEmpty(_localLobby.LobbyID))
            {
                Debug.Log("Already hosting or have a lobby—skipping Create.");
                return;
            }

            if (string.IsNullOrWhiteSpace(lobbyName))
                lobbyName = k_DefaultLobbyName;

            if (!await _auth.EnsurePlayerIsAuthorized())
            {
                Debug.LogWarning("Not authorized – aborting creation.");
                _joinOrCreateInProgress = false;
                UnblockUI();
                return;
            }

            // no Data payload at all, just a plain lobby
            var opts = new CreateLobbyOptions { IsPrivate = isPrivate };

            var attempt = await _lobbyFacade.TryCreateLobbyAsync(
                lobbyName,
                _selectedMaxPlayers,
                isPrivate,
                opts
            );

            if (!attempt.Success)
            {
                Debug.LogWarning($"CreateLobby failed: {attempt}");
                _joinOrCreateInProgress = false;
                UnblockUI();
                return;
            }

            var created = attempt.Lobby;
            _localUser.IsHost = true;
            _isLobbyHost = true;
            _lobbyFacade.SetRemoteLobby(created);

            Debug.Log($" Created lobby {created.Id}");
            await SetupRelayAndSaveToLobby(created);
            _connectionManager.StartHostLobby(_localUser.PlayerName);
            enabled = false;
            // leave _joinOrCreateInProgress true so no extra calls
            UnblockUI();
        }


        async System.Threading.Tasks.Task SetupRelayAndSaveToLobby(Lobby createdLobby)
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(_selectedMaxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log($"[Host] Relay Join Code: {relayJoinCode}");

                // ? Use lobby ID from the passed lobby (not _localLobby
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


        async void OnJoinedLobby(Lobby remote)
        {
            _lobbyFacade.SetRemoteLobby(remote);
            Debug.Log($"Joined lobby {remote.Id} (Code: {remote.LobbyCode}), starting client...");

            // ?? Client fetches Relay JoinCode
            if (remote.Data.TryGetValue("RelayJoinCode", out var relayJoinCodeData))
            {
                string relayJoinCode = relayJoinCodeData.Value;
                Debug.Log($"[Client] RelayJoinCode found: {relayJoinCode}");

                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1.5));

                await SetupRelayClient(relayJoinCode);

                //StartCoroutine(DelayedRelayJoin(relayJoinCode));

            }
            else
            {
                Debug.LogError("RelayJoinCode not found in lobby data!");
            }

        }

        async Task SetupRelayClient(string joinCode)
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

                    Debug.Log("[Client] Successfully joined Relay. Now starting client...");
                    await Task.Delay(1500);

                    // Add another safety: Check that transport config is not null
                    Debug.Log("Client Relay IP: " + transport.ConnectionData.Address);
                    _connectionManager.StartClientLobby(_localUser.PlayerName);
                    return;
                }
                catch (RelayServiceException e)
                {
                    Debug.LogWarning($"Relay join failed: {e.Message} — retries left: {retries}");
                    await System.Threading.Tasks.Task.Delay(1000);
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