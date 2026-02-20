using CosmicShore.App.Services;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    [DefaultExecutionOrder(0)]
    public class AppManager : SingletonNetworkPersistent<AppManager>
    {
        [SerializeField]
        AuthenticationDataVariable authenticationDataVariable;
        
        [SerializeField] private bool autoSignInAnnonymously;
        [SerializeField] private bool authenticationWithLog;
        
        AuthenticationServiceFacade authenticationServiceFacade;

        void Start()
        {
            authenticationServiceFacade = new(authenticationDataVariable, authenticationWithLog);
            authenticationServiceFacade.StartAuthentication();
        }
    }
}