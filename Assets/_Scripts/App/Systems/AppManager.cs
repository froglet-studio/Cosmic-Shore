using CosmicShore.Services;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using Resolution = Reflex.Enums.Resolution;

namespace CosmicShore.Systems
{
    [DefaultExecutionOrder(0)]
    public class AppManager : SingletonNetworkPersistent<AppManager>, IInstaller
    {
        [Header("Auth")]
        [SerializeField] AuthenticationDataVariable authenticationDataVariable;
        [SerializeField] bool authenticationWithLog;

        [Header("Network")]
        [SerializeField] NetworkMonitorDataVariable networkMonitorDataVariable;

        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        // ✅ Reflex will inject these from the container
        [Inject] AuthenticationServiceFacade authenticationServiceFacade;
        [Inject] NetworkMonitor networkMonitor;

        void Start()
        {
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
    }
}