using System;
using System.Threading;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Placed on the SplashScreen scene. After the splash finishes,
    /// checks if the user has a cached session. If so, goes directly to Menu_Main.
    /// Otherwise, loads the Authentication scene.
    ///
    /// Auth state is read from the <see cref="AuthenticationDataVariable"/> SOAP asset,
    /// which is updated by the <see cref="AuthenticationServiceFacade"/> started in AppManager.
    /// Uses SceneTransitionManager for fade transitions when available.
    /// </summary>
    public class SplashToAuthFlow : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string authSceneName = "Authentication";
        [SerializeField] private string mainMenuSceneName = "Menu_Main";

        [Header("Splash")]
        [SerializeField] private float splashDisplayDuration = 2f;

        [Header("Timeouts")]
        [SerializeField, Tooltip("Max seconds to wait for in-flight auth to complete.")]
        private float authWaitTimeout = 5f;

        [Inject] private AuthenticationDataVariable authenticationDataVariable;

        CancellationTokenSource _cts;

        void OnEnable()
        {
            _cts = new CancellationTokenSource();
        }

        void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void Start()
        {
            RunSplashFlowAsync(_cts.Token).Forget();
        }

        async UniTaskVoid RunSplashFlowAsync(CancellationToken ct)
        {
            try
            {
                // Show splash for the configured duration.
                await UniTask.Delay(
                    TimeSpan.FromSeconds(splashDisplayDuration),
                    ignoreTimeScale: true,
                    cancellationToken: ct);

                if (authenticationDataVariable == null)
                {
                    CSDebug.LogWarning("[SplashToAuthFlow] AuthenticationDataVariable not injected. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(authSceneName);
                    return;
                }

                var authData = authenticationDataVariable.Value;

                // AuthenticationServiceFacade may still be signing in.
                // Wait for in-flight auth to settle, with a timeout.
                if (authData.State == AuthenticationData.AuthState.Initializing ||
                    authData.State == AuthenticationData.AuthState.SigningIn)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(authWaitTimeout));

                    try
                    {
                        await UniTask.WaitUntil(
                            () => authData.IsSignedIn ||
                                  (authData.State != AuthenticationData.AuthState.Initializing &&
                                   authData.State != AuthenticationData.AuthState.SigningIn),
                            cancellationToken: timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        CSDebug.LogWarning("[SplashToAuthFlow] Auth wait timed out. Proceeding.");
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
            catch (OperationCanceledException) { /* scene destroyed — expected */ }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
                await LoadSceneWithTransitionAsync(authSceneName);
            }
        }

        async UniTask LoadSceneWithTransitionAsync(string sceneName)
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
