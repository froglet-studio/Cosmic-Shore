using System.Collections.Generic;
using JetBrains.Annotations;
using CosmicShore.Utility.Singleton;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using Event = Unity.Services.Analytics.Event;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class UnityAnalytics : SingletonPersistent<UnityAnalytics>
    {
        private bool _isConsented = true;
        private bool _isConnected = true;
        // Start is called before the first frame update
        async void Start()
        {
            await UnityServices.InitializeAsync();
            AskForConsents();
            SetUserId();
        }

        private void OnEnable()
        {
            NetworkMonitor.NetworkConnectionFound += OnNetworkEnabled;
            NetworkMonitor.NetworkConnectionLost += OnNetworkDisabled;
        }

        private void OnDisable()
        {
            NetworkMonitor.NetworkConnectionFound -= OnNetworkEnabled;
            NetworkMonitor.NetworkConnectionLost -= OnNetworkDisabled;
        }

        /// <summary>
        /// Ask For Consents - Async
        /// Ask User consents for data collection
        /// </summary>
        private async void AskForConsents()
        {
            try
            {
                await UnityServices.InitializeAsync();

// Show UI element asking the user for their consent OR retrieve prior consent from storage //

                if (_isConsented)
                {
                    AnalyticsService.Instance.StartDataCollection();
                }
            }
            catch (ConsentCheckException e)
            {
                Debug.LogWarningFormat("{0} - {1} - Handling consent error {2}", nameof(UnityAnalytics), nameof(AskForConsents), e.InnerException?.Message);
            }
            
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
            if (!_isConnected || !_isConsented) return;
            UnityServices.ExternalUserId = SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// Log Firebase Events
        /// </summary>
        /// <param name="eventName">Event Name</param>
        /// <param name="dict">Event Parameters and Values</param>
        public void LogCustomEvent(Event e)
        {
            AnalyticsService.Instance.RecordEvent(e);
        }

        public void LogCustomEventByName(string eventName)
        {
            AnalyticsService.Instance.RecordEvent(eventName);
        }

        /// <summary>
        /// Force Upload Data 
        /// Force upload data to Unity Analytics Services if network detected and is user consented
        /// </summary>
        public void ForceUploadData()
        {
            if (!_isConnected || !_isConsented) return;
            AnalyticsService.Instance.Flush();
        }
    }
}
