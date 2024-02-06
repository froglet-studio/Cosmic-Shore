using PlayFab;
using VContainer.Unity;

namespace CosmicShore.Integrations.PlayFabV2.Controllers
{
    public class UserProfileHandler : IInitializable
    {
        readonly private PlayFabClientInstanceAPI _clientInstance;

        public UserProfileHandler(PlayFabClientInstanceAPI clientInstance)
        {
            _clientInstance = clientInstance;
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public void UpdateDisplayName()
        {
            
        }

    }
}
