using System;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Core
{
    /// <summary>
    /// Persistent scene transition service. Manages a full-screen overlay for
    /// fade transitions between scenes.
    ///
    /// Supports two overlay modes:
    ///   1. External splash overlay — use the Bootstrap scene's branded Canvas
    ///      (background image + "LOADING" text). Wire to _splashOverlay.
    ///   2. Programmatic fallback — auto-creates a solid-color overlay if no
    ///      splash is wired.
    ///
    /// Also supports:
    ///   - Local async scene loading with fade in/out
    ///   - Network scene loading (server-authoritative via Netcode)
    ///   - Manual fade control for custom sequences
    ///
    /// Place on the Bootstrap persistent root. Registered in Reflex DI via AppManager.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SceneTransitionManager : MonoBehaviour
    {
        [Header("Splash Overlay (Optional)")]
        [SerializeField, Tooltip("External CanvasGroup to use as the scene transition overlay " +
            "(e.g., the Bootstrap splash screen). If set, the programmatic overlay is not created. " +
            "The referenced Canvas is made persistent and given highest sort order.")]
        CanvasGroup _splashOverlay;

        [Header("Fade Settings")]
        [SerializeField, Tooltip("Duration of the fade-to-black and fade-from-black transitions.")]
        float _fadeDuration = 0.4f;

        [SerializeField, Tooltip("Color of the full-screen fade overlay (only used when no splash overlay is wired).")]
        Color _fadeColor = Color.black;

        [Header("Timing")]
        [SerializeField, Tooltip("Brief pause after scene load before fading in, letting the new scene's Awake/Start run.")]
        float _postLoadSettleDelay = 0.1f;

        [Header("Overlay Message (Optional)")]
        [SerializeField, Tooltip("Optional TMP_Text on the overlay, shown while the screen is faded to " +
            "black (e.g. the Shuffle running standings between games). Reuse the existing loading-panel " +
            "TMP_Text (or any TMP_Text on the overlay). Leave null to disable — SetOverlayMessage then " +
            "no-ops. When the overlay fades back from black the text is restored to its authored content " +
            "(captured at Awake), so reusing a label like \"LOADING\" is safe.")]
        TMP_Text _overlayMessageText;

        CanvasGroup _fadeCanvasGroup;
        Canvas _fadeCanvas;
        bool _isTransitioning;
        CancellationTokenSource _cts;

        // The wired message text's authored content, captured at Awake. ClearOverlayMessage restores
        // this (not empty) so reusing an existing loading-panel TMP_Text doesn't lose its default label.
        string _overlayMessageDefault = string.Empty;

        /// <summary>
        /// True while a scene transition is in progress.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Fired after a scene finishes loading and the fade-in begins.
        /// </summary>
        public event Action<string> OnSceneLoadComplete;

        #region Unity Lifecycle

        void Awake()
        {
            _cts = new CancellationTokenSource();

            if (_splashOverlay != null)
                AdoptSplashOverlay();
            else
                CreateFadeOverlay();

            // Remember the message text's authored content so ClearOverlayMessage can restore it
            // (we may be reusing an existing loading-panel TMP_Text rather than a dedicated one).
            if (_overlayMessageText != null)
                _overlayMessageDefault = _overlayMessageText.text;
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Public API — Scene Loading

        /// <summary>
        /// Load a scene locally with fade transitions.
        /// </summary>
        public async UniTask LoadSceneAsync(string sceneName, bool fadeOut = true, bool fadeIn = true)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning($"[SceneTransition] Already transitioning. Ignoring request for '{sceneName}'.");
                return;
            }

            _isTransitioning = true;

            // Guard against _cts being null (e.g. if OnDestroy ran on a stale instance
            // during shutdown). Without this, _cts.Token below would NRE and leave the
            // fade overlay stuck opaque with the scene never loading.
            if (_cts == null)
                _cts = new CancellationTokenSource();

            try
            {
                var ct = _cts.Token;

                if (fadeOut)
                    await FadeAsync(0f, 1f, ct);

                var op = SceneManager.LoadSceneAsync(sceneName);
                if (op == null)
                {
                    // Unity returns null when the scene isn't in build settings or when
                    // called at a disallowed lifecycle moment. Fall back to a synchronous
                    // load so we never leave the player staring at a black overlay.
                    Debug.LogError($"[SceneTransition] SceneManager.LoadSceneAsync('{sceneName}') " +
                                   "returned null. Falling back to synchronous load.");
                    SceneManager.LoadScene(sceneName);
                }
                else
                {
                    await op.ToUniTask(cancellationToken: ct);
                }

                // Let the new scene's Awake/Start complete.
                if (_postLoadSettleDelay > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_postLoadSettleDelay),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: ct);
                }

                OnSceneLoadComplete?.Invoke(sceneName);

                if (fadeIn)
                    await FadeAsync(1f, 0f, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneTransition] LoadSceneAsync failed for '{sceneName}': {ex}. " +
                               "Clearing overlay and attempting direct load.");

                // Clear the overlay so the user isn't stuck on a black screen.
                SetFadeImmediate(0f);

                // If the target scene hasn't loaded yet, try a plain synchronous load.
                if (SceneManager.GetActiveScene().name != sceneName)
                {
                    try { SceneManager.LoadScene(sceneName); }
                    catch (Exception loadEx)
                    {
                        Debug.LogError($"[SceneTransition] Synchronous fallback also failed for " +
                                       $"'{sceneName}': {loadEx}");
                    }
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Load a scene through the Netcode SceneManager (server-authoritative).
        /// Falls back to local load if NetworkManager isn't available.
        /// </summary>
        public async UniTask LoadNetworkSceneAsync(string sceneName)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning($"[SceneTransition] Already transitioning. Ignoring request for '{sceneName}'.");
                return;
            }

            _isTransitioning = true;

            if (_cts == null)
                _cts = new CancellationTokenSource();

            try
            {
                var ct = _cts.Token;

                await FadeAsync(0f, 1f, ct);

                var nm = NetworkManager.Singleton;

                if (nm != null && nm.IsServer && nm.SceneManager != null)
                {
                    Debug.Log($"[SceneTransition] Server loading network scene: {sceneName}");
                    nm.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

                    // Network scene loads are asynchronous on the server.
                    // Wait until the active scene name matches the target.
                    await UniTask.WaitUntil(
                        () => SceneManager.GetActiveScene().name == sceneName,
                        cancellationToken: ct);
                }
                else if (nm != null && nm.IsClient)
                {
                    // Clients don't initiate network scene loads — the server drives them.
                    // We can still show the fade; the server will trigger the actual load.
                    Debug.LogWarning("[SceneTransition] Client cannot initiate network scene load. Waiting for server.");
                    await UniTask.WaitUntil(
                        () => SceneManager.GetActiveScene().name == sceneName,
                        cancellationToken: ct);
                }
                else
                {
                    // No NetworkManager — fall back to local load.
                    Debug.LogWarning("[SceneTransition] No NetworkManager. Falling back to local load.");
                    await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: ct);
                }

                if (_postLoadSettleDelay > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_postLoadSettleDelay),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: ct);
                }

                OnSceneLoadComplete?.Invoke(sceneName);

                await FadeAsync(1f, 0f, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _isTransitioning = false;
            }
        }

        #endregion

        #region Public API — Manual Fade Control

        /// <summary>
        /// Fade the overlay to fully opaque (black screen).
        /// </summary>
        public async UniTask FadeToBlack()
            => await FadeAsync(0f, 1f, EnsureCtsToken());

        /// <summary>
        /// Fade the overlay to fully transparent (reveal scene).
        /// </summary>
        public async UniTask FadeFromBlack()
            => await FadeAsync(1f, 0f, EnsureCtsToken());

        CancellationToken EnsureCtsToken()
        {
            if (_cts == null)
                _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        /// <summary>
        /// Set the overlay alpha immediately without animation.
        /// 0 = fully transparent, 1 = fully opaque.
        /// </summary>
        public void SetFadeImmediate(float alpha)
        {
            // Defensive guard: a UGS-SDK await resuming on the ThreadPool can land here,
            // and any UnityEngine.Object access (incl. `== null`) throws
            // EnsureRunningOnMainThread. Bail loudly instead of crashing the scene flow.
            if (!MainThreadDispatcher.IsOnMainThread)
            {
                Debug.LogError(
                    "[SceneTransitionManager] SetFadeImmediate called off main thread — " +
                    "caller forgot `.AsMainThread()` on a UGS / Netcode Task await " +
                    "(see UniTaskExtensions.cs). Ignoring to avoid EnsureRunningOnMainThread.");
                return;
            }

            if (_fadeCanvasGroup == null) return;
            _fadeCanvasGroup.alpha = alpha;
            _fadeCanvasGroup.blocksRaycasts = alpha > 0.01f;
            _fadeCanvasGroup.interactable = alpha > 0.01f;
        }

        #endregion

        #region Public API — Overlay Message

        /// <summary>
        /// Sets a message on the overlay (e.g. the Shuffle running standings) that is visible while the
        /// overlay is faded to black. No-ops if no <see cref="_overlayMessageText"/> is wired. The text
        /// auto-clears when the overlay next fades back from black (see <see cref="FadeAsync"/>), so the
        /// caller only ever needs to set it. Empty/null text shows nothing.
        /// </summary>
        public void SetOverlayMessage(string message)
        {
            // Mirror SetFadeImmediate: touching a UnityEngine.Object off the main thread (e.g. from a
            // UGS/Netcode continuation that forgot .AsMainThread()) throws EnsureRunningOnMainThread.
            if (!MainThreadDispatcher.IsOnMainThread)
            {
                Debug.LogError("[SceneTransitionManager] SetOverlayMessage called off main thread — " +
                               "ignoring to avoid EnsureRunningOnMainThread (caller forgot `.AsMainThread()`).");
                return;
            }

            if (_overlayMessageText == null) return;
            _overlayMessageText.text = message ?? string.Empty;
        }

        /// <summary>
        /// Restores the overlay message text to its authored content (captured at Awake). Called
        /// automatically when the overlay fades from black, so a reused loading-panel label is preserved.
        /// </summary>
        public void ClearOverlayMessage()
        {
            if (!MainThreadDispatcher.IsOnMainThread) return;
            if (_overlayMessageText == null) return;
            _overlayMessageText.text = _overlayMessageDefault;
        }

        #endregion

        #region Internal — Fade Animation

        async UniTask FadeAsync(float from, float to, CancellationToken ct)
        {
            if (_fadeCanvasGroup == null) return;

            _fadeCanvasGroup.alpha = from;
            _fadeCanvasGroup.blocksRaycasts = true;
            _fadeCanvasGroup.interactable = true;

            if (_fadeDuration <= 0f)
            {
                _fadeCanvasGroup.alpha = to;
                _fadeCanvasGroup.blocksRaycasts = to > 0.01f;
                _fadeCanvasGroup.interactable = to > 0.01f;
                if (to <= 0.01f) ClearOverlayMessage();   // overlay revealed → drop any message
                return;
            }

            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                _fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / _fadeDuration));
                await UniTask.Yield(ct);
            }

            _fadeCanvasGroup.alpha = to;
            _fadeCanvasGroup.blocksRaycasts = to > 0.01f;
            _fadeCanvasGroup.interactable = to > 0.01f;
            if (to <= 0.01f) ClearOverlayMessage();   // overlay revealed → drop any message
        }

        #endregion

        #region Internal — Overlay Construction

        /// <summary>
        /// Uses an existing scene Canvas (e.g., the Bootstrap splash screen) as the
        /// persistent transition overlay. Makes it DontDestroyOnLoad and sets highest
        /// sort order so it renders on top of all game content.
        /// </summary>
        void AdoptSplashOverlay()
        {
            _fadeCanvasGroup = _splashOverlay;

            var canvas = _splashOverlay.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 32767;
                DontDestroyOnLoad(canvas.gameObject);
                _fadeCanvas = canvas;
            }

            // interactable mirrors blocksRaycasts everywhere this group is driven:
            // the adopted splash hosts the BootStatusPanel retry button, which must
            // be tappable while the overlay is visible. The only Selectable under
            // either overlay variant is that button, and BootStatusPanel keeps it
            // inactive outside of BootStatusMode.Retry.
            _fadeCanvasGroup.alpha = 1f;
            _fadeCanvasGroup.blocksRaycasts = true;
            _fadeCanvasGroup.interactable = true;
        }

        void CreateFadeOverlay()
        {
            // Root canvas — screen-space overlay, highest sort order.
            var canvasGO = new GameObject("[SceneTransition_Overlay]");
            canvasGO.transform.SetParent(transform, false);

            _fadeCanvas = canvasGO.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 32767;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Full-screen image with CanvasGroup for alpha control.
            var imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(canvasGO.transform, false);

            var rt = imageGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = imageGO.AddComponent<Image>();
            image.color = _fadeColor;
            image.raycastTarget = true;

            _fadeCanvasGroup = imageGO.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.blocksRaycasts = false;
            _fadeCanvasGroup.interactable = false;
        }

        #endregion
    }
}
