using UnityEngine;

namespace CosmicShore.Integrations.Architectures.ChainResponsibilities
{
    public class AuthViewTest : MonoBehaviour
    {
        private void Start()
        {
            // Initiate handlers
            var authHandler = new AuthenticationHandler();
            var errorHandler = new ErrorHandler();
                
            // Set the responsibility chain
            authHandler.SetNext(errorHandler);
                
            Debug.Log("Chain: Auth -> Error");
            AuthenticationManagerTest.DoRequests(authHandler);
        }
    }
}