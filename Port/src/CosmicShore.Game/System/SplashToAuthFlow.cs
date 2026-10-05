// Ported verbatim from Assets/_Scripts/System/SplashToAuthFlow.cs
// (bootstrap arc 2026-07-08). Mechanical substitutions (README):
// Cysharp.Threading.Tasks → System.Threading.Tasks + CosmicShore.Engine.Tasks
// (UniTaskVoid/UniTask → Task + .Forget(); UniTask.Delay(TimeSpan,
// ignoreTimeScale: true, ct) → GameTask.Delay(seconds, unscaledTime: true, ct);
// UniTask.WaitUntil(pred, ct) → GameTask.WaitUntil(pred, ct));
// Reflex.Attributes → CosmicShore.Engine.Injection; UnityEngine →
// CosmicShore.Engine; UnityEngine.SceneManagement → CosmicShore.Engine
// .SceneManagement. FULLY LIVE — zero deviations: the splash hold, the
// in-flight-auth settle wait with timeout, and the always-route-through-
// Authentication load via SceneTransitionManager (local fallback included).

using System;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;


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
        [Header("Splash")]
        [SerializeField] private float splashDisplayDuration = 2f;

        [Header("Timeouts")]
        [SerializeField, Tooltip("Max seconds to wait for in-flight auth to complete.")]
        private float authWaitTimeout = 5f;

        [Inject] private AuthenticationDataVariable authenticationDataVariable;
        [Inject] private SceneNameListSO _sceneNames;
        [Inject] private SceneTransitionManager _sceneTransitionManager;

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

        async Task RunSplashFlowAsync(CancellationToken ct)
        {
            try
            {
                // Show splash for the configured duration.
                await GameTask.Delay(splashDisplayDuration, unscaledTime: true, cancellationToken: ct);

                if (authenticationDataVariable == null)
                {
                    CSDebug.LogWarning("[SplashToAuthFlow] AuthenticationDataVariable not injected. Going to auth scene.");
                    await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
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
                        await GameTask.WaitUntil(
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

                // Always route through the Authentication scene. Even when already
                // signed in, the auth scene ensures the network host is started and
                // loads Menu_Main via Netcode scene management, which is required for
                // player spawning (OnNetworkSpawn).
                if (authData.IsSignedIn)
                    CSDebug.Log("[SplashToAuthFlow] Already signed in. Routing through auth scene for network setup.");
                else
                    CSDebug.Log("[SplashToAuthFlow] Not signed in. Going to auth scene.");

                await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
            }
            catch (OperationCanceledException) { /* scene destroyed — expected */ }
            catch (Exception ex)
            {
                CSDebug.LogWarning($"[SplashToAuthFlow] Error during splash flow: {ex.Message}. Falling back to auth scene.");
                await LoadSceneWithTransitionAsync(_sceneNames.AuthenticationScene);
            }
        }

        async Task LoadSceneWithTransitionAsync(string sceneName)
        {
            if (_sceneTransitionManager != null)
            {
                await _sceneTransitionManager.LoadSceneAsync(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
