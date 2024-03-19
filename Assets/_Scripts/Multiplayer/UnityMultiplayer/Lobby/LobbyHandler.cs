using System;
using System.Threading.Tasks;
using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Utility.Singleton;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Multiplayer.UnityMultiplayer.Lobby
{
    
    public class LobbyHandler : SingletonPersistent<LobbyHandler>
    {
        #region Properties

        [Header("Lobby General Info")]
        [SerializeField] private string lobbyName = "Lobby";
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private string keyJoinCode = "RelayJoinCode";
        [SerializeField] private bool isPrivate = false;
        
        [Header("Lobby Intervals")]
        [SerializeField] private float lobbyHeartbeatInterval = 20.0f;
        [SerializeField] private float lobbyPollInterval = 65.0f;
        
        [Header("Connection Properties")]
        [SerializeField] private EncryptionType encryption = EncryptionType.DTLS;

        private string _playerId;
        private string _displayName;
        
        // Lobby
        private Unity.Services.Lobbies.Models.Lobby _currentLobby;
        
        // Current connection type
        private string _connectionType => encryption == EncryptionType.DTLS ? EncryptionDtls : EncryptionWws;
        
        // Const Definitions
        // Connection Types
        private const string EncryptionDtls = "dtls";
        private const string EncryptionWws = "wws";

        #endregion
        
        private async void Start()
        {
            await Authenticate();
        }

        #region Authentication

        private async Task Authenticate()
        {
            var displayName = $"Pilot{Random.Range(0, 100)}";
            await InitService(displayName);
            await SignIn(displayName);
        }

        private async Task InitService(string displayName)
        {
            Debug.Log($"Lobby Handler - profile name: {displayName}");
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                InitializationOptions options = new();
                options.SetProfile(displayName);

                await UnityServices.InitializeAsync(options);
                Debug.Log("Lobby Handler - InitService - Unity Service Initialized.");
            }
        }

        private async Task SignIn(string displayName)
        {
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log($"Lobby Handler - SignIn() - Signed in as {displayName}");
            };

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                _displayName = AuthenticationService.Instance.PlayerId;
            }
        }

        #endregion
        
        

    }
}
