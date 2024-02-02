using PlayFab;
using PlayFab.ClientModels;
using VContainer.Unity;

namespace CosmicShore
{
    public class AuthHandler : IInitializable
    {
        private readonly PlayFabAuth _playFabAuth;

        public AuthHandler(PlayFabAuth playFabAuth)
        {
            _playFabAuth = playFabAuth;
        }
        
        public void Initialize()
        {
            var request = new LoginWithCustomIDRequest();
            PlayFabClientAPI.LoginWithCustomID(request,
                result =>
                {
                    _playFabAuth.Id = result.PlayFabId;
                },
            error => { }
            );
        }

        
        
    }
}
