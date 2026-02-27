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

        [Header("Singleton Persistent Prefabs")]
        [SerializeField] GameSetting gameSettingPrefab;
        [SerializeField] AudioSystem audioSystemPrefab;
        [SerializeField] PlayerDataService playerDataServicePrefab;
        [SerializeField] UGSStatsManager ugsStatsManagerPrefab;
        [SerializeField] CaptainManager captainManagerPrefab;
        [SerializeField] IAPManager iapManagerPrefab;
        [SerializeField] GameManager gameManagerPrefab;
        [SerializeField] ThemeManager themeManagerPrefab;
        [SerializeField] CameraManager cameraManagerPrefab;
        [SerializeField] PostProcessingManager postProcessingManagerPrefab;
        [SerializeField] StatsManager statsManagerPrefab;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        // Runtime instances spawned from prefabs above.
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
            SpawnSingletonPersistents();
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

        void SpawnSingletonPersistents()
        {
            if (_resolved) return;
            _resolved = true;

            _gameSetting = SpawnPersistent(gameSettingPrefab, nameof(gameSettingPrefab));
            _audioSystem = SpawnPersistent(audioSystemPrefab, nameof(audioSystemPrefab));
            _playerDataService = SpawnPersistent(playerDataServicePrefab, nameof(playerDataServicePrefab));
            _ugsStatsManager = SpawnPersistent(ugsStatsManagerPrefab, nameof(ugsStatsManagerPrefab));
            _captainManager = SpawnPersistent(captainManagerPrefab, nameof(captainManagerPrefab));
            _iapManager = SpawnPersistent(iapManagerPrefab, nameof(iapManagerPrefab));
            _gameManager = SpawnPersistent(gameManagerPrefab, nameof(gameManagerPrefab));
            _themeManager = SpawnPersistent(themeManagerPrefab, nameof(themeManagerPrefab));
            _cameraManager = SpawnPersistent(cameraManagerPrefab, nameof(cameraManagerPrefab));
            _postProcessingManager = SpawnPersistent(postProcessingManagerPrefab, nameof(postProcessingManagerPrefab));
            _statsManager = SpawnPersistent(statsManagerPrefab, nameof(statsManagerPrefab));
        }

        static T SpawnPersistent<T>(T prefab, string fieldName) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError($"[AppManager] {fieldName} is not assigned.");
                return null;
            }

            var instance = Instantiate(prefab);
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Guarantee singletons are spawned before registration. Reflex may
            // call InstallBindings before Awake, so we cannot rely on Awake alone.
            SpawnSingletonPersistents();

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
