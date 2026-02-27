using System;
using System.Diagnostics;
using System.Threading;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Debug = UnityEngine.Debug;
using Resolution = Reflex.Enums.Resolution;

namespace CosmicShore.Core
{
    /// <summary>
    /// Top-level application orchestrator. Lives in the Bootstrap scene (build index 0).
    ///
    /// Responsibilities:
    ///   1. Establish the DontDestroyOnLoad root for all persistent objects.
    ///   2. Configure platform settings (framerate, vsync, screen sleep).
    ///   3. Resolve and register all persistent managers via Reflex DI.
    ///   4. Start authentication and network monitoring.
    ///   5. Transition to the first gameplay scene.
    ///
    /// Execution order is set to -100 so Awake/Start run before all other scripts,
    /// including SceneTransitionManager (-50) and AudioSystem (-1).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AppManager : MonoBehaviour, IInstaller
    {
        [Header("Bootstrap Configuration")]
        [SerializeField, Tooltip("Bootstrap settings asset. Create via ScriptableObjects/Core/BootstrapConfig.")]
        BootstrapConfigSO _bootstrapConfig;

        [Header("Scene Names")]
        [SerializeField, Tooltip("Centralized scene name list. Registered in DI for all consumers.")]
        SceneNameListSO _sceneNames;

        [Header("Scene Transition")]
        [SerializeField, Tooltip("Optional CanvasGroup for a fade-out effect before scene load.")]
        CanvasGroup _splashCanvasGroup;

        [SerializeField, Tooltip("Duration of the fade-out transition in seconds.")]
        float _fadeOutDuration = 0.5f;

        [Header("Auth")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;
        [SerializeField] bool authenticationWithLog;

        [Header("Network")]
        [SerializeField] NetworkMonitorDataVariable networkMonitorDataVariable;

        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Header("Lifecycle Events")]
        [SerializeField, Tooltip("SOAP event container for application lifecycle events (pause, focus, quit, scene load/unload).")]
        ApplicationLifecycleEventsContainerSO lifecycleEvents;

        [Header("Singleton Persistent Scene References")]
        [SerializeField] GameSetting gameSetting;
        [SerializeField] AudioSystem audioSystem;
        [SerializeField] PlayerDataService playerDataService;
        [SerializeField] UGSStatsManager ugsStatsManager;
        [SerializeField] CaptainManager captainManager;
        [SerializeField] IAPManager iapManager;
        [SerializeField] SceneLoader sceneLoader;
        [SerializeField] ThemeManager themeManager;
        [SerializeField] CameraManager cameraManager;
        [SerializeField] PostProcessingManager postProcessingManager;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        static bool _hasBootstrapped;
        bool _resolved;
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

            DontDestroyOnLoad(gameObject);
            ConfigurePlatform();
            TryResolveManagersEarly();
        }

        void Start()
        {
            ConfigureGameData();
            StartNetworkMonitor();
            StartAuthentication();

            _cts = new CancellationTokenSource();
            RunBootstrapAsync(_cts.Token).Forget();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void OnDisable() => Shutdown();
        void OnApplicationQuit() => Shutdown();

        void Shutdown()
        {
            StopNetworkMonitor();
            gameData?.ResetAllData();
        }

        #endregion

        #region Platform Configuration

        void ConfigurePlatform()
        {
            if (_bootstrapConfig == null)
            {
                Debug.LogWarning("[AppManager] No BootstrapConfigSO assigned. Using defaults.");
                Application.targetFrameRate = 60;
                return;
            }

            if (_bootstrapConfig.TargetFrameRate > 0)
                Application.targetFrameRate = _bootstrapConfig.TargetFrameRate;

            QualitySettings.vSyncCount = _bootstrapConfig.VSyncCount;

            if (_bootstrapConfig.PreventScreenSleep)
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

                // Wait one more frame so any Start()-driven systems settle.
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

                // Enforce minimum splash duration.
                // When auto-created (no config), use a short default so existing
                // services like auth have time to start.
                const float DefaultMinSplash = 0.5f;
                float elapsed = (float)stopwatch.Elapsed.TotalSeconds;
                float remaining = (_bootstrapConfig != null ? _bootstrapConfig.MinimumSplashDuration : DefaultMinSplash) - elapsed;
                if (remaining > 0f)
                {
                    Log($"Holding splash for {remaining:F2}s");
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(remaining),
                        DelayType.UnscaledDeltaTime,
                        cancellationToken: ct);
                }

                // Fade out splash if present.
                if (_splashCanvasGroup != null)
                    await FadeOutSplashAsync(ct);

                // Mark complete and transition.
                _hasBootstrapped = true;
                stopwatch.Stop();
                Log($"Bootstrap complete in {stopwatch.Elapsed.TotalSeconds:F2}s");

                OnBootstrapComplete?.Invoke();

                string targetScene = _sceneNames != null ? _sceneNames.AuthenticationScene : "Authentication";
                Log($"Loading scene: {targetScene}");

                // Use SceneTransitionManager if available (provides fade transitions).
                if (ServiceLocator.TryGet<SceneTransitionManager>(out var transitionManager))
                    await transitionManager.LoadSceneAsync(targetScene);
                else
                    SceneManager.LoadScene(targetScene);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[AppManager] Bootstrap sequence cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AppManager] Fatal bootstrap error: {ex}");
                OnBootstrapFailed?.Invoke(ex.Message);
            }
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

        #region Manager Resolution & DI

        /// <summary>
        /// Best-effort early resolution of manager references from the scene.
        /// Finds unassigned managers via FindAnyObjectByType and marks them
        /// DontDestroyOnLoad. Does not warn on missing managers — the lazy
        /// DI factory handles that at injection time.
        /// </summary>
        void TryResolveManagersEarly()
        {
            if (_resolved) return;
            _resolved = true;

            TryResolveManager(ref gameSetting);
            TryResolveManager(ref audioSystem);
            TryResolveManager(ref playerDataService);
            TryResolveManager(ref ugsStatsManager);
            TryResolveManager(ref captainManager);
            TryResolveManager(ref iapManager);
            TryResolveManager(ref sceneLoader);
            TryResolveManager(ref themeManager);
            TryResolveManager(ref cameraManager);
            TryResolveManager(ref postProcessingManager);
        }

        void TryResolveManager<T>(ref T field) where T : Component
        {
            if (field == null)
            {
#if UNITY_2023_1_OR_NEWER
                field = FindAnyObjectByType<T>();
#else
                field = FindObjectOfType<T>();
#endif
            }

            if (field != null)
                EnsurePersistent(field);
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Best-effort early resolution. Reflex may call InstallBindings
            // before Awake, so we cannot rely on Awake alone.
            TryResolveManagersEarly();

            // ── ScriptableObject assets ──────────────────────────────────
            // Project-level assets wired via inspector. RegisterValue is the
            // correct Reflex API: the instance already exists and is immutable.
            RegisterAsset(builder, _sceneNames, nameof(_sceneNames));
            RegisterAsset(builder, gameData, nameof(gameData));
            RegisterAsset(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterAsset(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));
            RegisterAsset(builder, lifecycleEvents, nameof(lifecycleEvents));

            // ── MonoBehaviour singletons (lazy factory) ──────────────────
            // Scene-resolved managers may not exist in the Bootstrap scene at
            // registration time. RegisterFactory with Lazy resolution defers
            // the scene lookup until first [Inject] access, so registration
            // always succeeds.
            RegisterManagerSingleton<GameSetting>(builder, gameSetting);
            RegisterManagerSingleton<AudioSystem>(builder, audioSystem);
            RegisterManagerSingleton<PlayerDataService>(builder, playerDataService);
            RegisterManagerSingleton<UGSStatsManager>(builder, ugsStatsManager);
            RegisterManagerSingleton<CaptainManager>(builder, captainManager);
            RegisterManagerSingleton<IAPManager>(builder, iapManager);
            RegisterManagerSingleton<SceneLoader>(builder, sceneLoader);
            RegisterManagerSingleton<ThemeManager>(builder, themeManager);
            RegisterManagerSingleton<CameraManager>(builder, cameraManager);
            RegisterManagerSingleton<PostProcessingManager>(builder, postProcessingManager);

            // ── Pure C# service singletons ───────────────────────────────
            // Created by factory, no scene object needed. Lazy so they are
            // only instantiated when first injected.
            builder.RegisterFactory(
                _ => new AuthenticationServiceFacade(authenticationDataVariable, authenticationWithLog),
                lifetime: Lifetime.Singleton,
                resolution: Resolution.Lazy
            );

            builder.RegisterFactory(
                _ => new NetworkMonitor(networkMonitorDataVariable),
                lifetime: Lifetime.Singleton,
                resolution: Resolution.Lazy
            );
        }

        /// <summary>
        /// Registers a ScriptableObject asset via RegisterValue. These are
        /// project-level assets that must be wired in the inspector.
        /// Logs an error (fail-loud) if the asset is missing.
        /// </summary>
        static void RegisterAsset<T>(ContainerBuilder builder, T asset, string fieldName) where T : ScriptableObject
        {
            if (asset != null)
            {
                builder.RegisterValue(asset);
                return;
            }
            Debug.LogError($"[AppManager] {fieldName} ScriptableObject asset is not assigned — DI registration skipped.");
        }

        /// <summary>
        /// Registers a MonoBehaviour singleton via RegisterFactory with lazy
        /// resolution. The factory prefers the serialized/early-resolved reference;
        /// if that is null it falls back to a scene search at injection time.
        /// This ensures registration always succeeds even when the manager
        /// hasn't loaded into the scene yet.
        /// </summary>
        void RegisterManagerSingleton<T>(ContainerBuilder builder, T serializedRef) where T : Component
        {
            builder.RegisterFactory<T>(
                _ =>
                {
                    // Prefer the reference that was already resolved (serialized or early-found).
                    if (serializedRef != null)
                    {
                        EnsurePersistent(serializedRef);
                        return serializedRef;
                    }

                    // Deferred scene search at first injection time.
#if UNITY_2023_1_OR_NEWER
                    var found = FindAnyObjectByType<T>();
#else
                    var found = FindObjectOfType<T>();
#endif
                    if (found != null)
                    {
                        EnsurePersistent(found);
                        return found;
                    }

                    Debug.LogError($"[AppManager] {typeof(T).Name} not found at injection time — DI resolution failed.");
                    return null;
                },
                lifetime: Lifetime.Singleton,
                resolution: Resolution.Lazy
            );
        }

        static void EnsurePersistent(Component component)
        {
            if (!component.TryGetComponent<DontDestroyOnLoad>(out _))
                component.gameObject.AddComponent<DontDestroyOnLoad>();
        }

        #endregion

        #region Service Startup

        void StartNetworkMonitor() => networkMonitor?.StartMonitoring();
        void StopNetworkMonitor() => networkMonitor?.StopMonitoring();
        void StartAuthentication() => authenticationServiceFacade?.StartAuthentication();

        void ConfigureGameData()
        {
            if (!gameData)
            {
                Debug.LogError("[AppManager] gameData is not assigned — cannot configure game data.");
                return;
            }

            gameData.ResetAllData();

            // Set sane defaults; the actual game mode, player count, and
            // intensity are configured by PartyGameLauncher when the host
            // picks a mode and presses play.
            gameData.SelectedPlayerCount.Value = 1;
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.SelectedIntensity.Value = 1;
        }

        #endregion

        #region Logging

        void Log(string message)
        {
            if (_bootstrapConfig == null || _bootstrapConfig.VerboseLogging)
                Debug.Log($"[AppManager] {message}");
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
            if (FindAnyObjectByType<AppManager>() != null) return;
#else
            if (FindObjectOfType<AppManager>() != null) return;
#endif

            Debug.Log("[AppManager] No AppManager found in Bootstrap scene. Auto-creating flow objects.");

            var go = new GameObject("[BootstrapFlow]");

            // SceneTransitionManager must be added first so it registers in
            // ServiceLocator before AppManager.Start() runs.
            go.AddComponent<SceneTransitionManager>();
            go.AddComponent<ApplicationLifecycleManager>();
            go.AddComponent<AppManager>();

            // AppManager.Awake() handles DontDestroyOnLoad.
        }

        #endregion
    }
}
