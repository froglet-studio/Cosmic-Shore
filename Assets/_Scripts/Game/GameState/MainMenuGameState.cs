using CosmicShore.Game.UI;
using CosmicShore.Integrations;
using CosmicShore.Utilities;
using CosmicShore.Utilities.Network;
using System;
using Unity.Services.Authentication;
using UnityEngine;
using VContainer;
using VContainer.Unity;


namespace CosmicShore.Game.GameState
{
    /// <summary>
    /// Game logic that runs when sitting at the MainMenu. This is likely to be "nothing", as no game has been started.
    /// But it is nonetheless important to have a game state, as the GameStateBehaviour system requires that all scenes have states.
    /// </summary>
    /// <remarks>
    /// OnNetworkSpawn() won't ever run, because there is no network connection at the main menu screen.
    /// Fortunately we know you are a client, because all players are client when sitting at the main menu screen.
    /// </remarks>
    public class MainMenuGameState : GameStateBehaviour
    {
        [SerializeField]
        NameGenerationData _nameGenerationData;

        [SerializeField]
        LobbyUIMediator _lobbyUIMediator;

        [SerializeField]
        GameObject _signInSpinner;

        [Inject]
        SceneNameListSO _sceneNameList;

        [Inject]
        UnityAuthenticationServiceFacade _authServiceFacade;

        [Inject]
        LocalLobbyUser _localUser;

        [Inject]
        LocalLobby _localLobby;

        [Inject]
        ProfileManager _profileManager;

        public override GameState ActiveState => GameState.MainMenu;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponent(_nameGenerationData);
            builder.RegisterComponent(_lobbyUIMediator);
        }


        protected override void Awake()
        {
            base.Awake();
            _signInSpinner.SetActive(false);

            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                OnSignInFailed();
                return;
            }

            TrySignIn();
        }

        protected override void OnDestroy()
        {
            _profileManager.onProfileChanged -= OnProfileChanged;
            base.OnDestroy();
        }

        async void TrySignIn()
        {
            try
            {
                var unityAuthenticationInitOptions =
                    _authServiceFacade.GenerateAuthenticationOptions(_profileManager.Profile);

                await _authServiceFacade.InitializeAndSignInAsync(unityAuthenticationInitOptions);
                OnAuthSignIn();
                _profileManager.onProfileChanged += OnProfileChanged;
            }
            catch (Exception)
            {
                OnSignInFailed();
            }
        }

        void OnAuthSignIn()
        {
            _signInSpinner.SetActive(false);

            Debug.Log($"Signed in. Unity Player ID {AuthenticationService.Instance.PlayerId}");

            _localUser.ID = AuthenticationService.Instance.PlayerId;

            // The local LobbyUser object will be hooked into UI before the LocalLobby is populated during lobby join, so the LocalLobby must know about it already when that happens.
            _localLobby.AddUser(_localUser);
        }

        void OnSignInFailed()
        {
            Debug.LogError("Sign In with Unity Authentication Failed!");
            if (_signInSpinner)
            {
                _signInSpinner.SetActive(false);
            }
        }

        async void OnProfileChanged()
        {
            _signInSpinner.SetActive(true);
            await _authServiceFacade.SwitchProfileAndReSignInAsync(_profileManager.Profile);

            _signInSpinner.SetActive(false);

            Debug.Log($"Signed in. Unity Player ID {AuthenticationService.Instance.PlayerId}");

            // Updating LocalUser and LocalLobby
            _localLobby.RemoveUser(_localUser);
            _localUser.ID = AuthenticationService.Instance.PlayerId;
            _localLobby.AddUser(_localUser);
        }
    }
}
