using CosmicShore.NetworkManagement;
using CosmicShore.Utilities.Network;
using CosmicShore.Utilities;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using VContainer;
using UnityEngine.UI;
using CosmicShore.Integrations;



namespace CosmicShore.Game.UI
{
    public class LobbyUIMediator : MonoBehaviour
    {
        private const string DEFAULT_LOBBY_NAME = "NoName";

        [SerializeField]
        CanvasGroup _canvasGroup;

        [SerializeField]
        GameObject _loadingSpinner;

        [Header("Create Lobby")]

        [SerializeField]
        Toggle _createLobbyToggle; 

        [SerializeField]
        TMP_InputField _lobbyName;

        [Header("Join Lobby")]

        [SerializeField]
        Toggle _joinLobbyToggle;

        [SerializeField]
        TMP_InputField _lobbyCode;

        UnityAuthenticationServiceFacade _authenticationServiceFacade;
        LobbyServiceFacade _lobbyServiceFacade;
        LocalLobbyUser _localLobbyUser;
        LocalLobby _localLobby;
        ConnectionManager _connectionManager;
        NameGenerationData _nameGenerationData;
        ISubscriber<ConnectStatus> _connectStatusSubscriber;

        [Inject]
        void InjectDependenciesAndInitialize(
            UnityAuthenticationServiceFacade authenticationServiceFacade,
            LobbyServiceFacade lobbyServiceFacade,
            LocalLobbyUser localUser,
            LocalLobby localLobby,
            NameGenerationData nameGenerationData,
            ISubscriber<ConnectStatus> connectStatusSub,
            ConnectionManager connectionManager
        )
        {
            _authenticationServiceFacade = authenticationServiceFacade;
            _nameGenerationData = nameGenerationData;
            _localLobbyUser = localUser;
            _lobbyServiceFacade = lobbyServiceFacade;
            _localLobby = localLobby;
            _connectionManager = connectionManager;
            _connectStatusSubscriber = connectStatusSub;
            RegenerateName();

            _connectStatusSubscriber.Subscribe(OnConnectStatus);
        }

        private void Start()
        {
            _createLobbyToggle.isOn = true;
            _joinLobbyToggle.isOn = false;
        }

        private void OnDestroy()
        {
            if (_connectStatusSubscriber != null)
            {
                _connectStatusSubscriber.Unsubscribe(OnConnectStatusChanged);
            }
        }

        public async void CreateLobby()
        {
            // before sending request to lobby service, populate an empty lobby name, if necessary
            if (string.IsNullOrEmpty(_lobbyName.text))
            {
                _lobbyName.text = DEFAULT_LOBBY_NAME;
            }

            BlockUIWhileLoadingIsInProgress();

            bool playerIsAuthorized = await _authenticationServiceFacade.EnsurePlayerIsAuthorized();

            if (!playerIsAuthorized)
            {
                UnblockUIAfterLoadingIsComplete();
                return;
            }

            (bool Success, Lobby Lobby) lobbyCreationAttempt =
                await _lobbyServiceFacade.TryCreateLobbyAsync(_lobbyName.text, _connectionManager.MaxConnectedPlayers, false);      // is not private

            if (lobbyCreationAttempt.Success)
            {
                _localLobbyUser.IsHost = true;
                _lobbyServiceFacade.SetRemoteLobby(lobbyCreationAttempt.Lobby);

                Debug.Log($"Created lobby with ID: {_localLobby.LobbyID} and code {_localLobby.LobbyCode}");
                _connectionManager.StartHostLobby(_localLobbyUser.PlayerName);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }


        /// <summary>
        /// an asynchronous method that attempts to join a lobby using a provided lobby code. 
        /// It blocks the user interface during the process. If the player is not authorized, 
        /// it unblocks the user interface and returns. If the attempt to join the lobby is successful, 
        /// it calls the OnJoinedLobby method. If the attempt is not successful, it unblocks the user interface.
        /// </summary>
        public async void JoinLobbyWithCodeRequest()
        {
            BlockUIWhileLoadingIsInProgress();

            // before sending request to lobby service, populate an empty lobby name, if necessary
            if (string.IsNullOrEmpty(_lobbyCode.text))
            {
                Debug.LogError("Please enter a valid lobby code!");
                return;
            }

            (bool Success, Lobby Lobby) lobbyJoinAttempt = await _lobbyServiceFacade.TryJoinLobbyAsync(null, _lobbyCode.text);

            if (lobbyJoinAttempt.Success)
            {
                OnJoinedLobby(lobbyJoinAttempt.Lobby);
            }
            else
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        public void RegenerateName()
        {
            _localLobbyUser.PlayerName = _nameGenerationData.GenerateName();
            // _playerNameLabel.text = m_LocalUser.DisplayName;
        }

        private void BlockUIWhileLoadingIsInProgress()
        {
            _canvasGroup.interactable = false;
            _loadingSpinner.SetActive(true);
        }

        private void UnblockUIAfterLoadingIsComplete()
        {
            // this callback can happen after we've already switched to a different scene
            // in that case the canvas group would be null
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _loadingSpinner.SetActive(false);
            }
        }

        private void OnConnectStatusChanged(ConnectStatus status)
        {
            if (status is ConnectStatus.GenericDisconnect or ConnectStatus.StartClientFailed)
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }

        private void OnJoinedLobby(Lobby remoteLobby)
        {
            _lobbyServiceFacade.SetRemoteLobby(remoteLobby);
            Debug.Log($"Joined lobby with ID: {_localLobby.LobbyID} and Internal Relay join code {_localLobby.RelayJoinCode}");
            _connectionManager.StartClientLobby(_localLobbyUser.PlayerName);
        }

        void OnConnectStatus(ConnectStatus status)
        {
            if (status is ConnectStatus.GenericDisconnect or ConnectStatus.StartClientFailed)
            {
                UnblockUIAfterLoadingIsComplete();
            }
        }
    }
}