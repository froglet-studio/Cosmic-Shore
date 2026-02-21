using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Services.Auth
{
    /// <summary>
    /// Placed on the SplashScreen scene. After the splash finishes,
    /// checks if the user has a cached session. If so, goes directly to Menu_Main.
    /// Otherwise, loads the Authentication scene.
    /// </summary>
    public class SplashToAuthFlow : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string authSceneName = "Authentication";
        [SerializeField] private string mainMenuSceneName = "Menu_Main";

        [Header("Splash")]
        [SerializeField] private float splashDisplayDuration = 2f;

        [Header("Dependencies")]
        [SerializeField] private AuthenticationController authController;

        async void Start()
        {
            try
            {
                // Show splash for the configured duration
                await Task.Delay(TimeSpan.FromSeconds(splashDisplayDuration));

                if (authController == null)
                    authController = FindAnyObjectByType<AuthenticationController>();

                if (authController == null)
                {
                    Debug.LogWarning("[SplashToAuthFlow] No AuthenticationController found. Going to auth scene.");
                    SceneManager.LoadScene(authSceneName);
                    return;
                }

                // Try to sign in with cached credentials
                bool signedIn = await authController.TrySignInCachedAsync();

                if (signedIn)
                {
                    Debug.Log("[SplashToAuthFlow] Cached session valid. Going to main menu.");
                    SceneManager.LoadScene(mainMenuSceneName);
                }
                else
                {
                    Debug.Log("[SplashToAuthFlow] No cached session. Going to auth scene.");
                    SceneManager.LoadScene(authSceneName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
                SceneManager.LoadScene(authSceneName);
            }
        }
    }
}
