using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Integrations.PlayFabV2.Models;
using CosmicShore.Utility.ClassExtensions;
using PlayFab;
using PlayFab.MultiplayerModels;
using VContainer.Unity;

namespace CosmicShore
{
    public class LobbyHandler : IPostInitializable, IDisposable
    {

        private readonly PlayFabAccount _account;

        public LobbyHandler(PlayFabAccount account)
        {
            _account = account;
        }

        public void PostInitialize()
        {
            if (_account.AuthContext == null)
            {
                this.LogErrorWithClassMethod(
                    MethodBase.GetCurrentMethod()?.Name,
                    "Authentication failed or was not complete.");
            }
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

        public void CreateLobby(uint playerCount, Dictionary<string, string> lobbySettings)
        {
            var request = new CreateLobbyRequest();
            if(_account.IsHost) request.Owner.Id = _account.AuthContext.EntityId;

            request.MaxPlayers = playerCount;
            request.AuthenticationContext = _account.AuthContext;
            request.LobbyData = lobbySettings;
            
            PlayFabMultiplayerAPI.CreateLobby(request, 
                OnCreatingLobby, 
                error => { 
                    this.LogErrorWithClassMethod(
                        MethodBase.GetCurrentMethod()?.Name, 
                    error.GenerateErrorReport());});
        }

        private void OnCreatingLobby(CreateLobbyResult result)
        {
            // Do the creating lobby thing
            this.LogWithClassMethod(
                MethodBase.GetCurrentMethod()?.Name, 
                $"Lobby id: {result.LobbyId}");
        }
        
        
    }
}
