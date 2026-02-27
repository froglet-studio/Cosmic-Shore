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
        [SerializeField] CaptainManager captainManager;
        [SerializeField] IAPManager iapManager;

        [Header("Gameplay Managers")]
        [SerializeField] GameManager gameManager;
        [SerializeField] ThemeManager themeManager;
        [SerializeField] CameraManager cameraManager;
        [SerializeField] PostProcessingManager postProcessingManager;
        [SerializeField] StatsManager statsManager;

        [Header("Additional Persistent Prefabs")]
        [Tooltip("Prefabs that need to persist across scenes but are not registered in DI. " +
                 "If an instance of the prefab's primary component already exists in the scene, " +
                 "that instance is reused instead of spawning a duplicate.")]
        [SerializeField] GameObject[] _additionalPersistentPrefabs;

        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        bool _persistentSystemsResolved;

        void Awake()
        {
            ResolvePersistentSystems();
            EnsureAdditionalPrefabs();
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
        /// Ensures all persistent system and gameplay manager references are available
        /// for DI registration. If a field is not assigned in the inspector (e.g. prefab
        /// override lost), auto-creates the service as a standalone persistent GameObject.
        /// Skips auto-creation when called on a prefab asset (non-scene context).
        /// </summary>
        void ResolvePersistentSystems()
        {
            if (_persistentSystemsResolved) return;
            _persistentSystemsResolved = true;

            gameSetting = EnsureService(gameSetting);
            audioSystem = EnsureService(audioSystem);
            playerDataService = EnsureService(playerDataService);
            ugsStatsManager = EnsureService(ugsStatsManager);
            captainManager = EnsureService(captainManager);
            iapManager = EnsureService(iapManager);

            gameManager = EnsureService(gameManager);
            themeManager = EnsureService(themeManager);
            cameraManager = EnsureService(cameraManager);
            postProcessingManager = EnsureService(postProcessingManager);
            statsManager = EnsureService(statsManager);
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

            // Guard: if we are a prefab asset (not a scene instance), we cannot
            // create GameObjects — skip auto-creation and let the service be
            // registered as null so RegisterIfNotNull logs the error.
            if (!gameObject.scene.IsValid())
            {
                Debug.LogWarning($"[AppManager] {typeof(T).Name} not assigned — skipping auto-create (prefab asset context).");
                return null;
            }

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
            RegisterIfNotNull(builder, captainManager, nameof(captainManager));
            RegisterIfNotNull(builder, iapManager, nameof(iapManager));

            // Gameplay managers
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

        #region Additional Persistent Prefabs

        /// <summary>
        /// Spawns additional persistent prefabs that are not registered in DI
        /// (e.g. PrismManagers, CallToActionManager, DailyChallengeSystem).
        /// For each prefab, checks if an instance of its primary component type
        /// already exists in the scene. If so, that instance is reused and persisted.
        /// Otherwise the prefab is instantiated and persisted.
        /// </summary>
        void EnsureAdditionalPrefabs()
        {
            if (_additionalPersistentPrefabs == null) return;

            foreach (var prefab in _additionalPersistentPrefabs)
            {
                if (prefab == null) continue;
                EnsurePrefabInstance(prefab);
            }
        }

        void EnsurePrefabInstance(GameObject prefab)
        {
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

        #endregion

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