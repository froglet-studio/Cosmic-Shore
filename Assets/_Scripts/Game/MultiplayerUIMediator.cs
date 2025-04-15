using CosmicShore.App.UI.Controllers;
using CosmicShore.Integrations;
using CosmicShore.NetworkManagement;
using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;



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
            // wire up your three room-size buttons
            _onePlayerBtn.onClick.AddListener(() => SelectPlayerCount(1));
            _twoPlayerBtn.onClick.AddListener(() => SelectPlayerCount(2));
            _threePlayerBtn.onClick.AddListener(() => SelectPlayerCount(3));
            // and the play button
            _playBtn.onClick.AddListener(JoinOrCreateLobby);

            // default selection
            SelectPlayerCount(1);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            _statusSub?.Unsubscribe(OnConnectStatus);
        }

        void SelectPlayerCount(int n)
        {
            _selectedMaxPlayers = n;
            // TODO: update button visuals to show which is selected
            Debug.Log($"Selected room size: {n}");
        }

        async void JoinOrCreateLobby()
        {
            BlockUI();
            bool ok = await _auth.EnsurePlayerIsAuthorized();
            if (!ok)
            {
                UnblockUI();
                return;
            }


            var result = await _lobbyFacade.TryQuickJoinLobbyAsync();

            if (result.Success)
            {
                OnJoinedLobby(result.Lobby);
            }
            else
            {
                Debug.LogWarning($"Quick join failed: {result}");
                OnQuickJoinFailed();
            }
        }

        void OnQuickJoinFailed()
        {
            CreateLobbyRequest();
        }

        async void CreateLobbyRequest(bool isPrivate = false, string lobbyName = null)
        {
            // before sending request to lobby service, populate an empty lobby name, if necessary
            if (string.IsNullOrEmpty(lobbyName))
            {
                lobbyName = k_DefaultLobbyName;
            }

            bool playerIsAuthorized = await _auth.EnsurePlayerIsAuthorized();

            if (!playerIsAuthorized)
            {
                UnblockUI();
                return;
            }

            var lobbyCreationAttempt = await _lobbyFacade.TryCreateLobbyAsync(lobbyName, _connectionManager.MaxConnectedPlayers, isPrivate);

            if (lobbyCreationAttempt.Success)
            {
                _localUser.IsHost = true;
                _lobbyFacade.SetRemoteLobby(lobbyCreationAttempt.Lobby);

                Debug.Log($"Created lobby with ID: {_localLobby.LobbyID} and code {_localLobby.LobbyCode}");
                _connectionManager.StartHostLobby(_localUser.PlayerName);
            }
            else
            {
                Debug.LogWarning($"Lobby creation failed! {lobbyCreationAttempt}");
                UnblockUI();
            }
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
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _loadingSpinner.SetActive(false);
            }
        }

        void OnConnectStatus(ConnectStatus status)
        {
            if (status is ConnectStatus.GenericDisconnect or ConnectStatus.StartClientFailed)
            {
                Debug.LogWarning($"Connnection status error: {status}");
            }

            if (status is ConnectStatus.Success)
            {
                // Do something when the connection is successful
            }
        }

        void OnJoinedLobby(Lobby remote)
        {
            _lobbyFacade.SetRemoteLobby(remote);

            Debug.Log($"Joined lobby {remote.Id}, starting client…");
            _connectionManager.StartClientLobby(_localUser.PlayerName);
        }
    }
}
