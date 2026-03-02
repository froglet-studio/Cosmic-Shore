using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        CanvasGroup _fadeCanvasGroup;
        Canvas _fadeCanvas;
        bool _isTransitioning;
        CancellationTokenSource _cts;

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

            try
            {
                var ct = _cts.Token;

                if (fadeOut)
                    await FadeAsync(0f, 1f, ct);

                await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: ct);

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
            => await FadeAsync(0f, 1f, _cts.Token);

        /// <summary>
        /// Fade the overlay to fully transparent (reveal scene).
        /// </summary>
        public async UniTask FadeFromBlack()
            => await FadeAsync(1f, 0f, _cts.Token);

        /// <summary>
        /// Set the overlay alpha immediately without animation.
        /// 0 = fully transparent, 1 = fully opaque.
        /// </summary>
        public void SetFadeImmediate(float alpha)
        {
            if (_fadeCanvasGroup == null) return;
            _fadeCanvasGroup.alpha = alpha;
            _fadeCanvasGroup.blocksRaycasts = alpha > 0.01f;
            _fadeCanvasGroup.interactable = false;
        }

        #endregion

        #region Internal — Fade Animation

        async UniTask FadeAsync(float from, float to, CancellationToken ct)
        {
            if (_fadeCanvasGroup == null) return;

            _fadeCanvasGroup.alpha = from;
            _fadeCanvasGroup.blocksRaycasts = true;
            _fadeCanvasGroup.interactable = false;

            if (_fadeDuration <= 0f)
            {
                _fadeCanvasGroup.alpha = to;
                _fadeCanvasGroup.blocksRaycasts = to > 0.01f;
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

            _fadeCanvasGroup.alpha = 1f;
            _fadeCanvasGroup.blocksRaycasts = true;
            _fadeCanvasGroup.interactable = false;
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
