using CosmicShore.UI;
using CosmicShore.Gameplay;
using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Resolution = Reflex.Enums.Resolution;

namespace CosmicShore.Core
{
    [DefaultExecutionOrder(0)]
    public class AppManager : MonoBehaviour, IInstaller
    {
        [Header("Auth")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;
        [SerializeField] bool authenticationWithLog;

        [Header("Network")]
        [SerializeField] NetworkMonitorDataVariable networkMonitorDataVariable;

        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Header("Singleton Persistents")]
        [Tooltip("Prefabs instantiated once during bootstrap and marked DontDestroyOnLoad. " +
                 "Components on each spawned instance are automatically discovered and " +
                 "registered in DI. Keep all service references here (not in the scene) " +
                 "for git-friendly prefab-to-prefab wiring.")]
        [SerializeField] GameObject[] _singletonPersistents;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        // Cached at spawn time via TryGetComponent — no scene scans needed.
        GameSetting _gameSetting;
        AudioSystem _audioSystem;
        PlayerDataService _playerDataService;
        UGSStatsManager _ugsStatsManager;
        CaptainManager _captainManager;
        IAPManager _iapManager;
        GameManager _gameManager;
        ThemeManager _themeManager;
        CameraManager _cameraManager;
        PostProcessingManager _postProcessingManager;
        StatsManager _statsManager;

        bool _resolved;

        void Awake()
        {
            SpawnAndResolveSingletonPersistents();
        }

        void Start()
        {
            ConfigureGameData();
            StartNetworkMonitor();
            StartAuthentication();
        }

        void OnDisable() => Shutdown();
        void OnApplicationQuit() => Shutdown();

        void Shutdown()
        {
            StopNetworkMonitor();
            gameData?.ResetAllData();
        }

        /// <summary>
        /// Instantiates each singleton persistent prefab, marks it
        /// DontDestroyOnLoad, and caches known service components directly
        /// from the spawned instance — zero FindFirstObjectByType calls.
        /// </summary>
        void SpawnAndResolveSingletonPersistents()
        {
            if (_resolved) return;
            _resolved = true;

            if (_singletonPersistents == null) return;

            foreach (var prefab in _singletonPersistents)
            {
                if (prefab == null) continue;

                var instance = Instantiate(prefab);
                DontDestroyOnLoad(instance);
                CacheServices(instance);
            }
        }

        void CacheServices(GameObject instance)
        {
            TryCache(instance, ref _gameSetting);
            TryCache(instance, ref _audioSystem);
            TryCache(instance, ref _playerDataService);
            TryCache(instance, ref _ugsStatsManager);
            TryCache(instance, ref _captainManager);
            TryCache(instance, ref _iapManager);
            TryCache(instance, ref _gameManager);
            TryCache(instance, ref _themeManager);
            TryCache(instance, ref _cameraManager);
            TryCache(instance, ref _postProcessingManager);
            TryCache(instance, ref _statsManager);
        }

        static void TryCache<T>(GameObject instance, ref T field) where T : Component
        {
            if (field == null)
                instance.TryGetComponent(out field);
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Guarantee singletons are spawned before registration. Reflex may
            // call InstallBindings before Awake, so we cannot rely on Awake alone.
            SpawnAndResolveSingletonPersistents();

            // ScriptableObject assets / Variables
            RegisterIfNotNull(builder, gameData, nameof(gameData));
            RegisterIfNotNull(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterIfNotNull(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));

            // Singleton persistent services (cached from spawned prefab instances)
            RegisterIfNotNull(builder, _gameSetting, nameof(_gameSetting));
            RegisterIfNotNull(builder, _audioSystem, nameof(_audioSystem));
            RegisterIfNotNull(builder, _playerDataService, nameof(_playerDataService));
            RegisterIfNotNull(builder, _ugsStatsManager, nameof(_ugsStatsManager));
            RegisterIfNotNull(builder, _captainManager, nameof(_captainManager));
            RegisterIfNotNull(builder, _iapManager, nameof(_iapManager));
            RegisterIfNotNull(builder, _gameManager, nameof(_gameManager));
            RegisterIfNotNull(builder, _themeManager, nameof(_themeManager));
            RegisterIfNotNull(builder, _cameraManager, nameof(_cameraManager));
            RegisterIfNotNull(builder, _postProcessingManager, nameof(_postProcessingManager));
            RegisterIfNotNull(builder, _statsManager, nameof(_statsManager));

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
            Debug.LogError($"[AppManager] {fieldName} is not assigned and could not be found — skipping DI registration.");
        }

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
    }
}
