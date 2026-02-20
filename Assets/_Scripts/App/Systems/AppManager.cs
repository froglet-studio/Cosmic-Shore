using System;
using CosmicShore.App.Services;
using CosmicShore.Soap;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.App.Systems
{
    [DefaultExecutionOrder(0)]
    public class AppManager : SingletonNetworkPersistent<AppManager>
    {
        [SerializeField]
        AuthenticationDataVariable authenticationDataVariable;
        
        [SerializeField]
        NetworkMonitorDataVariable networkMonitorDataVariable;
        
        [SerializeField]
        bool autoSignInAnnonymously;
        
        [SerializeField]
        bool authenticationWithLog;

        [SerializeField]
        GameDataSO gameData;
        
        AuthenticationServiceFacade authenticationServiceFacade;
        NetworkMonitor networkMonitor;

        void Start()
        {
            StartNetworkMonitor();
            StartAuthentication();
        }

        private void OnDisable()
        {
            StopNetworkMonitor();
            gameData.ResetAllData();
        }

        private void OnApplicationQuit()
        {
            StopNetworkMonitor();
            gameData.ResetAllData();
        }

        void StartNetworkMonitor()
        {
            var monitor = new NetworkMonitor(networkMonitorDataVariable);
            monitor.StartMonitoring();
        }

        void StopNetworkMonitor()
        {
            networkMonitor.StopMonitoring();
        }

        void StartAuthentication()
        {
            authenticationServiceFacade = new(authenticationDataVariable, authenticationWithLog);
            authenticationServiceFacade.StartAuthentication();
        }
    }
}