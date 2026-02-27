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
        [SerializeField] StatsManager statsManager;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        bool _validated;

        void Awake()
        {
            ValidatePersistents();
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

        void ValidatePersistents()
        {
            if (_validated) return;
            _validated = true;

            ValidatePersistent(gameSetting, nameof(gameSetting));
            ValidatePersistent(audioSystem, nameof(audioSystem));
            ValidatePersistent(playerDataService, nameof(playerDataService));
            ValidatePersistent(ugsStatsManager, nameof(ugsStatsManager));
            ValidatePersistent(captainManager, nameof(captainManager));
            ValidatePersistent(iapManager, nameof(iapManager));
            ValidatePersistent(gameManager, nameof(gameManager));
            ValidatePersistent(themeManager, nameof(themeManager));
            ValidatePersistent(cameraManager, nameof(cameraManager));
            ValidatePersistent(postProcessingManager, nameof(postProcessingManager));
            ValidatePersistent(statsManager, nameof(statsManager));
        }

        static void ValidatePersistent<T>(T reference, string fieldName) where T : Component
        {
            if (reference == null)
            {
                Debug.LogError($"[AppManager] {fieldName} is not assigned.");
                return;
            }

            if (!reference.TryGetComponent<DontDestroyOnLoad>(out _))
                Debug.LogError($"[AppManager] {fieldName} is missing a DontDestroyOnLoad component.");
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Guarantee validation runs before registration. Reflex may
            // call InstallBindings before Awake, so we cannot rely on Awake alone.
            ValidatePersistents();

            // ScriptableObject assets / Variables
            RegisterIfNotNull(builder, gameData, nameof(gameData));
            RegisterIfNotNull(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterIfNotNull(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));

            // Singleton persistent services (scene references)
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
            RegisterIfNotNull(builder, statsManager, nameof(statsManager));

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
