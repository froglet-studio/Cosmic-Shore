using CosmicShore.Services.Auth;
using CosmicShore.Utilities;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.UnityAnalytics
{
    public class UnityAnalytics : SingletonPersistent<UnityAnalytics>
    {
        private const bool IsConsented = true;

        private bool _isConnected = true;

        private async void Start()
        {
            try
            {
                // Use centralized UGS initialization via AuthenticationController
                if (AuthenticationController.Instance != null)
                    await AuthenticationController.Instance.EnsureInitializedAsync();
                else
                    await UnityServices.InitializeAsync(); // Fallback if no controller present

                AskForConsents();
                SetUserId();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UnityAnalytics] Failed to initialize: {ex.Message}");
            }
        }

        private void OnEnable()
        {
            NetworkMonitor.OnNetworkConnectionFound += OnNetworkEnabled;
            NetworkMonitor.OnNetworkConnectionLost += OnNetworkDisabled;
        }

        private void OnDisable()
        {
            NetworkMonitor.OnNetworkConnectionFound -= OnNetworkEnabled;
            NetworkMonitor.OnNetworkConnectionLost -= OnNetworkDisabled;
        }

        /// <summary>
        /// Ask For Consents - Async
        /// Ask User consents for data collection
        /// </summary>
        private void AskForConsents()
        {
            if(IsConsented && _isConnected) AnalyticsService.Instance.StartDataCollection();
            else AnalyticsService.Instance.StopDataCollection();
        }

        /// <summary>
        /// On Network Enabled Event
        /// Unity Analytics sends analytics events automatically
        /// TODO: additional operations can be done here upon enabling network
        /// </summary>
        private void OnNetworkEnabled()
        {
            _isConnected = true;
        }

        /// <summary>
        /// On Network Disabled Event
        /// Unity Analytics will cache events up to 5MB in memory until they're successfully uploaded
        /// Or the app is shut down.
        /// When the app is shut down, up to 5MB data will be saved on the disk.
        /// TODO: additional operations can be done here upon disabling network
        /// </summary>
        private void OnNetworkDisabled()
        {
            _isConnected = false;
        }

        /// <summary>
        /// Set User Id
        /// The unique identifier is current device unique identifier
        /// </summary>
        private void SetUserId()
        {
            if (!_isConnected || !IsConsented) return;
            UnityServices.ExternalUserId = SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// Force Upload Data 
        /// Force upload data to Unity Analytics Services if network detected and is user consented
        /// </summary>
        public void ForceUploadData()
        {
            if (!_isConnected || !IsConsented) return;
            AnalyticsService.Instance.Flush();
        }
    }
}
