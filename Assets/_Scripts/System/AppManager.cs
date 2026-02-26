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

        [Header("Persistent Systems")]
        [SerializeField] GameSetting gameSetting;
        [SerializeField] AudioSystem audioSystem;
        [SerializeField] PlayerDataService playerDataService;
        [SerializeField] UGSStatsManager ugsStatsManager;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        void Awake()
        {
            ResolvePersistentSystems();
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
        /// Ensures persistent system references are available for DI registration.
        /// If a field is not assigned in the inspector (e.g. prefab override lost),
        /// auto-creates the service as a child of this transform so it inherits
        /// DontDestroyOnLoad and gets registered in the Reflex container.
        /// </summary>
        void ResolvePersistentSystems()
        {
            gameSetting = EnsureService(gameSetting);
            audioSystem = EnsureService(audioSystem);
            playerDataService = EnsureService(playerDataService);
            ugsStatsManager = EnsureService(ugsStatsManager);
        }

        T EnsureService<T>(T field) where T : Component
        {
            // Use inspector-assigned reference if available
            if (field != null)
            {
                DontDestroyOnLoad(field.gameObject);
                return field;
            }

            // Look for an existing instance (e.g. persisted from bootstrap via DontDestroyOnLoad)
            var existing = FindFirstObjectByType<T>();
            if (existing != null) return existing;

            Debug.LogWarning($"[AppManager] {typeof(T).Name} not assigned and not found — auto-creating persistent instance.");
            var go = new GameObject($"[{typeof(T).Name}]");
            DontDestroyOnLoad(go);
            return go.AddComponent<T>();
        }

        public void InstallBindings(ContainerBuilder builder)
        {
            // Guarantee services exist before registration. Reflex may call
            // InstallBindings before Awake, so we cannot rely on Awake alone.
            ResolvePersistentSystems();

            // ScriptableObject assets / Variables
            RegisterIfNotNull(builder, gameData, nameof(gameData));
            RegisterIfNotNull(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterIfNotNull(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));

            // Persistent MonoBehaviour systems
            RegisterIfNotNull(builder, gameSetting, nameof(gameSetting));
            RegisterIfNotNull(builder, audioSystem, nameof(audioSystem));
            RegisterIfNotNull(builder, playerDataService, nameof(playerDataService));
            RegisterIfNotNull(builder, ugsStatsManager, nameof(ugsStatsManager));

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