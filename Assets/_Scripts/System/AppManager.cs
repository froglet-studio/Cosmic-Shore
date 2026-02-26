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
            builder.RegisterValue(gameData);
            builder.RegisterValue(authenticationDataVariable);
            builder.RegisterValue(networkMonitorDataVariable);

            // Persistent MonoBehaviour systems: register existing scene instances
            builder.RegisterValue(gameSetting);
            builder.RegisterValue(audioSystem);
            builder.RegisterValue(playerDataService);
            builder.RegisterValue(ugsStatsManager);

            // Persistent C# singletons (live as long as the RootScope container lives)
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

        void StartNetworkMonitor() => networkMonitor?.StartMonitoring();
        void StopNetworkMonitor() => networkMonitor?.StopMonitoring();
        void StartAuthentication() => authenticationServiceFacade?.StartAuthentication();

        void ConfigureGameData()
        {
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