using CosmicShore.App.UI.Controllers;
using CosmicShore.Integrations;
using CosmicShore.NetworkManagement;
using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
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
using static CosmicShore.Game.CharacterSelectController;

namespace CosmicShore.Game.UI
{
    public class MultiplayerUIMediator : MonoBehaviour
    {
        private const string DEFAULT_LOBBY_NAME = "MinigameFreestyleMultiplayer_Gameplay";
        private const string GAME_SCENE_NAME = "MinigameFreestyleMultiplayer_Gameplay";

        [Header("UI References")]
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] GameObject _loadingSpinner;
        [SerializeField] Button _onePlayerBtn;
        [SerializeField] Button _twoPlayerBtn;
        [SerializeField] Button _threePlayerBtn;
        [SerializeField] Button _playBtn;
        //[SerializeField] TMP_InputField _lobbyNameInput;   // optional: custom name
        //[SerializeField] TMP_InputField _lobbyCodeInput;   // for manual join (if you still want)

        public NetworkList<CharacterSelectData> CharacterSelections = new();

        // injected services
        UnityAuthenticationServiceFacade _auth;
        LobbyServiceFacade _lobbyFacade;
        LocalLobbyUser _localUser;
        LocalLobby _localLobby;
        ConnectionManager _connMgr;
        NameGenerationData _nameGen;
        ISubscriber<ConnectStatus> _statusSub;
        ICharacterSelectionController _charSelectController;

        [SerializeField]
        SO_ArcadeGame _selectedGame;

        int _selectedMaxPlayers = 1;

        [Inject]
        void Inject(
            UnityAuthenticationServiceFacade auth,
            LobbyServiceFacade lobbyFacade,
            LocalLobbyUser localUser,
            LocalLobby localLobby,
            NameGenerationData nameGen,
            ISubscriber<ConnectStatus> statusSub,
            ConnectionManager connMgr
            //ICharacterSelectionController charSelectController
        )
        {
            _auth = auth;
            _lobbyFacade = lobbyFacade;
            _localUser = localUser;
            _localLobby = localLobby;
            _connMgr = connMgr;
            _nameGen = nameGen;
            _statusSub = statusSub;
            //_charSelectController = charSelectController;


            RegenerateName();
            _statusSub.Subscribe(OnConnectStatus);
        }

        void Awake()
        {
            // Grab the ClassSelectionController that’s on the same GameObject
            _charSelectController = GetComponent<ClassSelectionController>();
            if (_charSelectController == null)
                Debug.LogError("You must have a ClassSelectionController on this GameObject!");
            _charSelectController.OnShipSelected += HandleShipSelected;

        }

        void Start()
        {
            // wire up your three room-size buttons
            _onePlayerBtn.onClick.AddListener(() => SelectSize(1));
            _twoPlayerBtn.onClick.AddListener(() => SelectSize(2));
            _threePlayerBtn.onClick.AddListener(() => SelectSize(3));
            // and the play button
            _playBtn.onClick.AddListener(JoinOrCreateLobby);

            _charSelectController.Initialize(_selectedGame.Captains);

            // default selection
            SelectSize(1);
        }

        void OnDestroy()
        {
            _statusSub?.Unsubscribe(OnConnectStatus);
        }

        void SelectSize(int n)
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

            // 1) Try to find an existing lobby with this max?players
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 5,
                Filters = new List<QueryFilter>
                {
                    // only lobbies that still have slots
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op:    QueryFilter.OpOptions.GT,
                        value: "0"
                    ),
                    // and whose MaxPlayers custom data matches our selection
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.S2,   // we’ll stash it in S2
                        op:    QueryFilter.OpOptions.EQ,
                        value: _selectedMaxPlayers.ToString()
                    )
                }
            };

            QueryResponse resp = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            if (resp.Results.Count > 0)
            {
                // join the first one
                var toJoin = resp.Results[0];
                Debug.Log($"Found lobby {toJoin.Id} with slots. Joining…");
                var joinResult = await _lobbyFacade.TryJoinLobbyAsync(toJoin.Id, null);
                if (joinResult.Success)
                {
                    OnJoinedLobby(joinResult.Lobby);
                    return;
                }
                else
                {
                    Debug.LogWarning("Failed to join existing lobby, will create new one.");
                }
            }

            // 2) no suitable lobby found (or join failed) -> create a fresh one
            //string lobbyName = string.IsNullOrEmpty(_lobbyNameInput.text)
            //    ? DEFAULT_LOBBY_NAME
            //    : _lobbyNameInput.text;

            // build custom lobby data so we can filter on MaxPlayers next time
            var data = new Dictionary<string, DataObject>
            {
                {
                    "MaxPlayers",
                    new DataObject(
                        visibility: DataObject.VisibilityOptions.Public,
                        value:      _selectedMaxPlayers.ToString()
                    )
                }
            };

            try
            {
                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
        {
            { "MaxPlayers", new DataObject(
                  visibility: DataObject.VisibilityOptions.Public,
                  value:      _selectedMaxPlayers.ToString(),
                  index:      DataObject.IndexOptions.N1
              )
            }
        }
                };

                string lobbyName = LobbyNameUtility.GenerateRandomLobbyName(10);

                // pass maxPlayers as the 2nd parameter, options as the 3rd
                Lobby created = await LobbyService.Instance
                    .CreateLobbyAsync(lobbyName, _selectedMaxPlayers, options);

                _localUser.IsHost = true;
                _lobbyFacade.SetRemoteLobby(created);
                _connMgr.StartHostLobby(_localUser.PlayerName);

                //OnJoinedLobby(created);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed to create lobby: {e.Message}");
                UnblockUI();
            }
        }

        public async void JoinLobbyWithCodeRequest()
        {
            BlockUI();
            if (string.IsNullOrEmpty(""))
            {
                Debug.LogError("Please enter a valid lobby code!");
                UnblockUI();
                return;
            }

            var joinAttempt = await _lobbyFacade.TryJoinLobbyAsync(null, "");
            if (joinAttempt.Success)
            {
                OnJoinedLobby(joinAttempt.Lobby);
            }
            else
            {
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
                UnblockUI();
        }

        [Inject] SceneNameListSO _sceneNameList;

        void OnJoinedLobby(Lobby remote)
        {
            _lobbyFacade.SetRemoteLobby(remote);
            Debug.Log($"Joined lobby {remote.Id}, starting client…");
            _connMgr.StartClientLobby(_localUser.PlayerName);

            // now load the game scene via your SceneLoaderWrapper
            if (SceneLoaderWrapper.Instance == null)
            {
                Debug.LogError("SceneLoaderWrapper.Instance is null! Cannot load scene.");
            }
            else
            {
                string sceneToLoad = _sceneNameList.CharSelectScene;
                Debug.Log($"Loading scene '{sceneToLoad}' via SceneLoaderWrapper…");
                SceneLoaderWrapper.Instance.LoadScene(sceneToLoad, true, showLoadingScreen: false);
            }
        }


        private void HandleShipSelected(int index)
        {
            // Delegate selection to server via existing method
            OnShipChoose(index);
        }

        // Retain only network RPCs; remove UI-specific methods
        public void OnShipChoose(int index)
        {
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            OnShipChoose_ServerRpc(index, clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnShipChoose_ServerRpc(int index, ulong clientId)
        {
            // existing server-side handling
            bool updated = false;
            for (int i = 0; i < CharacterSelections.Count; i++)
            {
                if (CharacterSelections[i].ClientId == clientId)
                {
                    var cs = CharacterSelections[i];
                    CharacterSelections[i] = new CharacterSelectData(clientId, index, cs.TeamIndex, cs.IsReady);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                CharacterSelections.Add(new CharacterSelectData(clientId, index, 0, false));
            }
        }
    }
}
