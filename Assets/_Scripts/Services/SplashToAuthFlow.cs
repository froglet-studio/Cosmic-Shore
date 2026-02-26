using System;
using System.Threading.Tasks;
using CosmicShore.Systems.Bootstrap;
using CosmicShore.Utility.DataContainers;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.App.Systems.Authentication;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Services
{
    /// <summary>
    /// Placed on the SplashScreen scene. After the splash finishes,
    /// checks if the user has a cached session. If so, goes directly to Menu_Main.
    /// Otherwise, loads the Authentication scene.
    ///
    /// Uses SceneTransitionManager for fade transitions when available.
    /// </summary>
    public class SplashToAuthFlow : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private SceneNameListSO _sceneNames;

        [Header("Splash")]
        [SerializeField] private float splashDisplayDuration = 2f;

        [Header("Dependencies")]
        [SerializeField] private AuthenticationController authController;

        async void Start()
        {
            try
            {
                // Show splash for the configured duration.
                await Task.Delay(TimeSpan.FromSeconds(splashDisplayDuration));

                if (authController == null)
                    authController = FindAnyObjectByType<AuthenticationController>();

                if (authController == null)
                {
                    CSDebug.LogWarning("[SplashToAuthFlow] No AuthenticationController found. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
                    return;
                }

                // Try to sign in with cached credentials.
                bool signedIn = await authController.TrySignInCachedAsync();

                if (signedIn)
                {
                    CSDebug.Log("[SplashToAuthFlow] Cached session valid. Going to main menu.");
                    await LoadSceneWithTransitionAsync(_sceneNames.MainMenuScene);
                }
                else
                {
                    CSDebug.Log("[SplashToAuthFlow] No cached session. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
                }
            }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
                await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
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
