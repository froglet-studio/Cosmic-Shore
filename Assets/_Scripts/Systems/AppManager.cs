using CosmicShore.App.Profile;
using CosmicShore.Core;
using CosmicShore.Game.Party;
using CosmicShore.Integrations.PlayFab.PlayerData;
using CosmicShore.Services;
using CosmicShore.Soap;
using CosmicShore.Systems.Audio;
using CosmicShore.Utilities;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace CosmicShore.Systems
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

        [Header("Player Services")]
        [SerializeField] PlayerDataService playerDataService;
        [SerializeField] PartyManager partyManager;
        [SerializeField] PlayerDataController playerDataController;

        [Header("UI")]
        [SerializeField] PopupManager popupManager;

        [Header("Game")]
        [SerializeField] CameraManager cameraManager;

        [SerializeField]
        string Main_Menu_Name = "MainMenuFreestyle";

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

            // Player services (persistent across scenes)
            if (playerDataService) builder.RegisterValue(playerDataService);
            if (partyManager) builder.RegisterValue(partyManager);
            if (playerDataController) builder.RegisterValue(playerDataController);

            // UI systems
            if (popupManager) builder.RegisterValue(popupManager);

            // Game systems
            if (cameraManager) builder.RegisterValue(cameraManager);

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
            gameData.SceneName = Main_Menu_Name;
            gameData.GameMode = GameModes.MultiplayerFreestyle;
            gameData.IsMultiplayerMode = true;
            gameData.SelectedPlayerCount.Value = 4;
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.SelectedIntensity.Value = 1;
        }
    }
}