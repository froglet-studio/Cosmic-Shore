using System;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Placed on the SplashScreen scene. After the splash finishes,
    /// checks if the user has a cached session. If so, goes directly to Menu_Main.
    /// Otherwise, loads the Authentication scene.
    ///
    /// Uses SceneTransitionManager for fade transitions when available.
    /// Auth state is read from the AuthenticationDataVariable SOAP asset,
    /// which is updated by the AuthenticationServiceFacade started in AppManager.
    /// </summary>
    public class SplashToAuthFlow : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string authSceneName = "Authentication";
        [SerializeField] private string mainMenuSceneName = "Menu_Main";

        [Header("Splash")]
        [SerializeField] private float splashDisplayDuration = 2f;

        [Header("Dependencies")]
        [Inject] private AuthenticationDataVariable authenticationDataVariable;

        async void Start()
        {
            try
            {
                // Show splash for the configured duration.
                await Task.Delay(TimeSpan.FromSeconds(splashDisplayDuration));

                if (authenticationDataVariable == null)
                {
                    CSDebug.LogWarning("[SplashToAuthFlow] AuthenticationDataVariable not injected. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(authSceneName);
                    return;
                }

                var authData = authenticationDataVariable.Value;

                // AuthenticationServiceFacade may still be signing in.
                // Wait briefly for in-flight auth to complete.
                if (authData.State == AuthenticationData.AuthState.Initializing ||
                    authData.State == AuthenticationData.AuthState.SigningIn)
                {
                    float waited = 0f;
                    const float maxWait = 5f;
                    while (!authData.IsSignedIn && waited < maxWait &&
                           (authData.State == AuthenticationData.AuthState.Initializing ||
                            authData.State == AuthenticationData.AuthState.SigningIn))
                    {
                        await Task.Delay(100);
                        waited += 0.1f;
                    }
                }

                if (authData.IsSignedIn)
                {
                    CSDebug.Log("[SplashToAuthFlow] Already signed in. Going to main menu.");
                    await LoadSceneWithTransitionAsync(mainMenuSceneName);
                }
                else
                {
                    CSDebug.Log("[SplashToAuthFlow] Not signed in. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(authSceneName);
                }
            }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
                await LoadSceneWithTransitionAsync(authSceneName);
            }
        }

        async Task LoadSceneWithTransitionAsync(string sceneName)
        {
            if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager))
            {
                await transitionManager.LoadSceneAsync(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
