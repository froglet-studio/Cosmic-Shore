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
        [Tooltip("Prefabs spawned during bootstrap and made persistent (DontDestroyOnLoad). " +
                 "All prefab references live here instead of in the scene for git-friendliness. " +
                 "If an instance of a prefab's primary component already exists, " +
                 "that instance is reused instead of spawning a duplicate.")]
        [SerializeField] GameObject[] _singletonPersistents;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        // Resolved at runtime after spawning singleton persistents
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
        /// Spawns all singleton persistent prefabs and discovers their components
        /// for DI registration. Each prefab is instantiated once and marked
        /// DontDestroyOnLoad. If an instance already exists in the scene, it is
        /// reused and persisted instead of spawning a duplicate.
        /// </summary>
        void SpawnAndResolveSingletonPersistents()
        {
            if (_resolved) return;
            _resolved = true;

            // Phase 1: Spawn all prefabs (or reuse existing instances)
            if (_singletonPersistents != null)
            {
                foreach (var prefab in _singletonPersistents)
                {
                    if (prefab == null) continue;
                    EnsurePrefabInstance(prefab);
                }
            }

            // Phase 2: Discover spawned components for DI registration
            _gameSetting = FindFirstObjectByType<GameSetting>();
            _audioSystem = FindFirstObjectByType<AudioSystem>();
            _playerDataService = FindFirstObjectByType<PlayerDataService>();
            _ugsStatsManager = FindFirstObjectByType<UGSStatsManager>();
            _captainManager = FindFirstObjectByType<CaptainManager>();
            _iapManager = FindFirstObjectByType<IAPManager>();
            _gameManager = FindFirstObjectByType<GameManager>();
            _themeManager = FindFirstObjectByType<ThemeManager>();
            _cameraManager = FindFirstObjectByType<CameraManager>();
            _postProcessingManager = FindFirstObjectByType<PostProcessingManager>();
            _statsManager = FindFirstObjectByType<StatsManager>();
        }

        void EnsurePrefabInstance(GameObject prefab)
        {
            // Check if an instance of any root-level MonoBehaviour already exists.
            foreach (var mb in prefab.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var existing = FindFirstObjectByType(mb.GetType()) as Component;
                if (existing != null)
                {
                    DontDestroyOnLoad(existing.transform.root.gameObject);
                    return;
                }
            }

            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
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

            // Singleton persistent services
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
