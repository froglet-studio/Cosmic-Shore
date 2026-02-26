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

        // ✅ Reflex will inject these from the container
        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

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

        public void InstallBindings(ContainerBuilder builder)
        {
            // ScriptableObject assets / Variables: register as values (singleton bindings)
            RegisterIfNotNull(builder, gameData, nameof(gameData));
            RegisterIfNotNull(builder, authenticationDataVariable, nameof(authenticationDataVariable));
            RegisterIfNotNull(builder, networkMonitorDataVariable, nameof(networkMonitorDataVariable));

            // Persistent MonoBehaviour systems: register existing scene instances
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
            Debug.LogError($"[AppManager] {fieldName} is not assigned in the inspector — skipping DI registration.");
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