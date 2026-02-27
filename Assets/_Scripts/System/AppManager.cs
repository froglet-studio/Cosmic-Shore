using System;
using System.Collections.Generic;
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
    ///   4. Initialize IBootstrapService implementations in declared order.
    ///   5. Start authentication and network monitoring.
    ///   6. Transition to the first gameplay scene.
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

        [Header("Persistent Root")]
        [SerializeField, Tooltip("The root GameObject that receives DontDestroyOnLoad. " +
                                  "All persistent services should be children of this object. " +
                                  "If null, this GameObject is used.")]
        Transform _persistentRoot;

        [Header("Bootstrap Services")]
        [SerializeField, Tooltip("MonoBehaviours implementing IBootstrapService. " +
                                  "Initialized in list order during bootstrap.")]
        List<MonoBehaviour> _bootstrapServices = new();

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

        [Header("Singleton Persistent Scene References")]
        [SerializeField] GameSetting gameSetting;
        [SerializeField] AudioSystem audioSystem;
        [SerializeField] PlayerDataService playerDataService;
        [SerializeField] UGSStatsManager ugsStatsManager;
        [SerializeField] CaptainManager captainManager;
        [SerializeField] IAPManager iapManager;
        [SerializeField] GameManager gameManager;
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

            SetupPersistentRoot();
            ConfigurePlatform();
            ResolveAndValidateManagers();
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

        void SetupPersistentRoot()
        {
            // Use a local so we never write back to the serialized field at runtime,
            // which would mark the scene dirty.
            var root = _persistentRoot != null ? _persistentRoot : transform;
            DontDestroyOnLoad(root.gameObject);
        }

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

                // Phase 1: Initialize services in declared order.
                await InitializeServicesAsync(ct);

                // Phase 2: Wait one more frame so any Start()-driven systems settle.
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

                // Phase 3: Enforce minimum splash duration.
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

                // Phase 4: Fade out splash if present.
                if (_splashCanvasGroup != null)
                    await FadeOutSplashAsync(ct);

                // Phase 5: Mark complete and transition.
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

        async UniTask InitializeServicesAsync(CancellationToken ct)
        {
            if (_bootstrapServices == null || _bootstrapServices.Count == 0)
            {
                Log("No IBootstrapService entries. Skipping service initialization phase.");
                return;
            }

            float timeout = _bootstrapConfig != null ? _bootstrapConfig.ServiceInitTimeoutSeconds : 15f;
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
                    Debug.LogWarning($"[AppManager] '{mb.name}' does not implement IBootstrapService. Skipping.");
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
                        Debug.LogWarning($"[AppManager] {service.ServiceName} returned without error but reports not initialized.");
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    Debug.LogError($"[AppManager] Timeout ({timeout}s) reached during {service.ServiceName}.");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AppManager] {service.ServiceName} failed: {ex.Message}");
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

        #region Manager Resolution & DI

        /// <summary>
        /// Resolves any unassigned manager references by finding them in the scene,
        /// then validates that each resolved manager has DontDestroyOnLoad.
        ///
        /// This handles the case where AppManager lives on a prefab and cannot hold
        /// serialized references to scene MonoBehaviours. Serialized fields still work
        /// as the primary binding when wired via scene overrides — FindAnyObjectByType
        /// is only the fallback.
        /// </summary>
        void ResolveAndValidateManagers()
        {
            if (_resolved) return;
            _resolved = true;

            ResolveManager(ref gameSetting, nameof(gameSetting));
            ResolveManager(ref audioSystem, nameof(audioSystem));
            ResolveManager(ref playerDataService, nameof(playerDataService));
            ResolveManager(ref ugsStatsManager, nameof(ugsStatsManager));
            ResolveManager(ref captainManager, nameof(captainManager));
            ResolveManager(ref iapManager, nameof(iapManager));
            ResolveManager(ref gameManager, nameof(gameManager));
            ResolveManager(ref themeManager, nameof(themeManager));
            ResolveManager(ref cameraManager, nameof(cameraManager));
            ResolveManager(ref postProcessingManager, nameof(postProcessingManager));
        }

        /// <summary>
        /// If the serialized field is null, attempts to find the manager in the scene.
        /// When found, ensures it has a DontDestroyOnLoad component so it persists
        /// across scene transitions.
        /// </summary>
        void ResolveManager<T>(ref T field, string fieldName) where T : Component
        {
            if (field == null)
            {
#if UNITY_2023_1_OR_NEWER
                field = FindAnyObjectByType<T>();
#else
                field = FindObjectOfType<T>();
#endif
            }

            if (field == null)
            {
                Debug.LogWarning($"[AppManager] {fieldName} not found in scene — will skip DI registration.");
                return;
            }

            if (!field.TryGetComponent<DontDestroyOnLoad>(out _))
                field.gameObject.AddComponent<DontDestroyOnLoad>();
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Resolve scene managers before registration. Reflex may call
            // InstallBindings before Awake, so we cannot rely on Awake alone.
            ResolveAndValidateManagers();

            // ScriptableObject assets / Variables
            RegisterIfNotNull(builder, _sceneNames, nameof(_sceneNames));
            RegisterIfNotNull(builder, gameData, nameof(gameData));
            RegisterIfNotNull(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterIfNotNull(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));

            // Singleton persistent services (resolved from scene)
            RegisterIfNotNull(builder, gameSetting, nameof(gameSetting));
            RegisterIfNotNull(builder, audioSystem, nameof(audioSystem));
            RegisterIfNotNull(builder, playerDataService, nameof(playerDataService));
            RegisterIfNotNull(builder, ugsStatsManager, nameof(ugsStatsManager));
            RegisterIfNotNull(builder, captainManager, nameof(captainManager));
            RegisterIfNotNull(builder, iapManager, nameof(iapManager));
            RegisterIfNotNull(builder, gameManager, nameof(gameManager));
            RegisterIfNotNull(builder, themeManager, nameof(themeManager));
            RegisterIfNotNull(builder, cameraManager, nameof(cameraManager));
            RegisterIfNotNull(builder, postProcessingManager, nameof(postProcessingManager));

            // Persistent C# singletons (live as long as the RootScope container lives)
            if (authenticationDataVariable != null)
            {
                builder.RegisterFactory(
                    _ => new AuthenticationServiceFacade(authenticationDataVariable, authenticationWithLog),
                    lifetime: Lifetime.Singleton,
                    resolution: Resolution.Lazy
                );
            }

            if (networkMonitorDataVariable != null)
            {
                builder.RegisterFactory(
                    _ => new NetworkMonitor(networkMonitorDataVariable),
                    lifetime: Lifetime.Singleton,
                    resolution: Resolution.Lazy
                );
            }
        }

        static void RegisterIfNotNull<T>(ContainerBuilder builder, T value, string fieldName) where T : class
        {
            if (value != null)
            {
                builder.RegisterValue(value);
                return;
            }
            Debug.LogWarning($"[AppManager] {fieldName} is not available — skipping DI registration.");
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
