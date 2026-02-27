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

        bool _resolved;

        void Awake()
        {
            ResolveAndValidateManagers();
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
            ResolveManager(ref statsManager, nameof(statsManager));
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
            Debug.LogWarning($"[AppManager] {fieldName} is not available — skipping DI registration.");
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
