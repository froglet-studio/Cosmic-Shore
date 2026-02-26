using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using DG.Tweening;

namespace CosmicShore.Systems.Bootstrap
{
    /// <summary>
    /// Top-level bootstrap orchestrator. Lives in the Bootstrap scene (build index 0).
    ///
    /// Responsibilities:
    ///   1. Establish the DontDestroyOnLoad root for all persistent objects.
    ///   2. Configure platform settings (framerate, vsync, screen sleep).
    ///   3. Initialize IBootstrapService implementations in declared order.
    ///   4. Validate service readiness with configurable timeout.
    ///   5. Transition to the first gameplay scene.
    ///
    /// Execution order is set to -100 so Awake/Start run before all other scripts,
    /// including AppManager (0) and AudioSystem (-1).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BootstrapController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Bootstrap settings asset. Create via ScriptableObjects/Core/BootstrapConfig.")]
        BootstrapConfigSO _config;

        [Header("Persistent Root")]
        [SerializeField, Tooltip("The root GameObject that receives DontDestroyOnLoad. " +
                                  "All persistent services should be children of this object. " +
                                  "If null, this GameObject is used.")]
        Transform _persistentRoot;

        [Header("Bootstrap Services")]
        [SerializeField, Tooltip("MonoBehaviours implementing IBootstrapService. " +
                                  "Initialized in list order during bootstrap. " +
                                  "AppManager and other Reflex-bound services don't need to be here " +
                                  "unless they implement IBootstrapService.")]
        List<MonoBehaviour> _bootstrapServices = new();

        [Header("Scene Transition")]
        [SerializeField, Tooltip("Optional CanvasGroup for a fade-out effect before scene load.")]
        CanvasGroup _splashCanvasGroup;

        [SerializeField, Tooltip("Duration of the fade-out transition in seconds.")]
        float _fadeOutDuration = 0.5f;

        static bool _hasBootstrapped;
        CancellationTokenSource _cts;

        /// <summary>
        /// Fired after all services initialize and before the first scene loads.
        /// </summary>
        public static event Action OnBootstrapComplete;

        /// <summary>
        /// Fired if bootstrap encounters a fatal error. Passes the error message.
        /// </summary>
        public static event Action<string> OnBootstrapFailed;

        /// <summary>
        /// Whether the bootstrap sequence has completed at least once this session.
        /// </summary>
        public static bool HasBootstrapped => _hasBootstrapped;

        #region Unity Lifecycle

        void Awake()
        {
            // Guard against re-entry (e.g., scene reload in editor).
            if (_hasBootstrapped)
            {
                Log("Already bootstrapped this session. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            SetupPersistentRoot();
            ConfigurePlatform();
        }

        void Start()
        {
            _cts = new CancellationTokenSource();
            RunBootstrapAsync(_cts.Token).Forget();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region Platform Configuration

        void SetupPersistentRoot()
        {
            if (_persistentRoot == null)
                _persistentRoot = transform;

            DontDestroyOnLoad(_persistentRoot.gameObject);
        }

        void ConfigurePlatform()
        {
            if (_config == null)
            {
                Debug.LogWarning("[Bootstrap] No BootstrapConfigSO assigned. Using defaults.");
                Application.targetFrameRate = 60;
                return;
            }

            if (_config.TargetFrameRate > 0)
                Application.targetFrameRate = _config.TargetFrameRate;

            QualitySettings.vSyncCount = _config.VSyncCount;

            if (_config.PreventScreenSleep)
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        #endregion

        #region Bootstrap Sequence

        async UniTaskVoid RunBootstrapAsync(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            Log("Bootstrap sequence starting...");

            try
            {
                // Wait one frame so all Awake() calls complete across the scene.
                await UniTask.Yield(PlayerLoopTiming.PreUpdate, ct);

                // Phase 1: Initialize services in declared order.
                await InitializeServicesAsync(ct);

                // Phase 2: Wait one more frame so any Start()-driven systems settle.
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

                // Phase 3: Enforce minimum splash duration.
                // When auto-created (no config), use a short default so existing
                // services like AppManager and UGS auth have time to start.
                const float DefaultMinSplash = 0.5f;
                float elapsed = (float)stopwatch.Elapsed.TotalSeconds;
                float remaining = (_config != null ? _config.MinimumSplashDuration : DefaultMinSplash) - elapsed;
                if (remaining > 0f)
                {
                    Log($"Holding splash for {remaining:F2}s");
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(remaining),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: ct);
                }

                // Phase 4: Fade out splash if present.
                if (_splashCanvasGroup != null)
                    await FadeOutSplashAsync(ct);

                // Phase 5: Mark complete and transition.
                _hasBootstrapped = true;
                stopwatch.Stop();
                Log($"Bootstrap complete in {stopwatch.Elapsed.TotalSeconds:F2}s");

                OnBootstrapComplete?.Invoke();

                string targetScene = _config != null ? _config.FirstSceneName : "Authentication";
                Log($"Loading scene: {targetScene}");

                // Use SceneTransitionManager if available (provides fade transitions).
                if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager))
                    await transitionManager.LoadSceneAsync(targetScene);
                else
                    SceneManager.LoadScene(targetScene);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[Bootstrap] Sequence cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bootstrap] Fatal error: {ex}");
                OnBootstrapFailed?.Invoke(ex.Message);
            }
        }

        async UniTask InitializeServicesAsync(CancellationToken ct)
        {
            if (_bootstrapServices == null || _bootstrapServices.Count == 0)
            {
                Log("No IBootstrapService entries. Skipping service initialization phase.");
                return;
            }

            float timeout = _config != null ? _config.ServiceInitTimeoutSeconds : 15f;
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var linkedToken = linkedCts.Token;

            int initialized = 0;
            int total = 0;

            foreach (var mb in _bootstrapServices)
            {
                if (mb == null) continue;

                if (mb is not IBootstrapService service)
                {
                    Debug.LogWarning($"[Bootstrap] '{mb.name}' does not implement IBootstrapService. Skipping.");
                    continue;
                }

                total++;
                Log($"  Initializing: {service.ServiceName}");
                var sw = Stopwatch.StartNew();

                try
                {
                    await service.InitializeAsync(linkedToken);
                    sw.Stop();

                    if (service.IsInitialized)
                    {
                        initialized++;
                        Log($"  {service.ServiceName} ready ({sw.ElapsedMilliseconds}ms)");
                    }
                    else
                    {
                        Debug.LogWarning($"[Bootstrap] {service.ServiceName} returned without error but reports not initialized.");
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    Debug.LogError($"[Bootstrap] Timeout ({timeout}s) reached during {service.ServiceName}.");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Bootstrap] {service.ServiceName} failed: {ex.Message}");
                }
            }

            Log($"Service initialization complete: {initialized}/{total} services ready.");
        }

        async UniTask FadeOutSplashAsync(CancellationToken ct)
        {
            if (_splashCanvasGroup == null || _fadeOutDuration <= 0f)
                return;

            float elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                _splashCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / _fadeOutDuration);
                await UniTask.Yield(ct);
            }

            _splashCanvasGroup.alpha = 0f;
        }

        #endregion

        #region Logging

        void Log(string message)
        {
            if (_config == null || _config.VerboseLogging)
                Debug.Log($"[Bootstrap] {message}");
        }

        #endregion

        #region Static Safety Net

        /// <summary>
        /// Ensures the Bootstrap scene has loaded in standalone builds.
        /// In the editor, the existing SceneBootstrapper handles this via InitializeOnLoad.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureBootstrapOnStartup()
        {
            // Reset between domain reloads in the editor.
            _hasBootstrapped = false;
        }

        /// <summary>
        /// Auto-creates the bootstrap flow objects in the Bootstrap scene when they
        /// are not already placed in the scene hierarchy. This bridges the gap between
        /// the code-defined flow and scenes that haven't been manually updated yet.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreateBootstrapFlow()
        {
            if (_hasBootstrapped) return;

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex != 0) return;

#if UNITY_2023_1_OR_NEWER
            if (FindAnyObjectByType<BootstrapController>() != null) return;
#else
            if (FindObjectOfType<BootstrapController>() != null) return;
#endif

            Debug.Log("[Bootstrap] No BootstrapController found in Bootstrap scene. Auto-creating flow objects.");

            var go = new GameObject("[BootstrapFlow]");

            // SceneTransitionManager must be added first so it registers in
            // ServiceLocator before BootstrapController.Start() runs.
            go.AddComponent<SceneTransitionManager>();
            go.AddComponent<ApplicationLifecycleManager>();
            go.AddComponent<BootstrapController>();

            // BootstrapController.Awake() handles DontDestroyOnLoad.
        }

        #endregion
    }
}
