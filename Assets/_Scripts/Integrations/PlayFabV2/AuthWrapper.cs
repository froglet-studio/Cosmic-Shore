using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using VContainer.Unity;

namespace CosmicShore
{
    public class AuthWrapper : IInitializable
    {
        private readonly UserAuth _userAuth;

        public AuthWrapper(UserAuth userAuth)
        {
            _userAuth = userAuth;
        }
        
        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        private void LoginWithDeviceId(UserActions<LoginWithCustomIDRequest, LoginResult> action)
        {
            var request = new LoginWithCustomIDRequest();
            request.CreateAccount = true;
            request.CustomId = SystemInfo.deviceUniqueIdentifier;
            
            PlayFabClientAPI.LoginWithCustomID(
                request,
                action.OnSuccess,
                action.OnFailed
                );
        }
    }
}
