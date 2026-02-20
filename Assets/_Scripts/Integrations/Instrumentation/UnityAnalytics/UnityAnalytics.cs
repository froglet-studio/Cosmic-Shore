using CosmicShore.Utilities;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace CosmicShore.Integrations.Instrumentation.UnityAnalytics
{
    public class UnityAnalytics : MonoBehaviour
    {
        [SerializeField]
        NetworkMonitorDataVariable  _networkMonitorDataVariable;
        NetworkMonitorData _networkMonitorData => _networkMonitorDataVariable.Value;
        
        [SerializeField]
        AuthenticationDataVariable  _authenticationDataVariable;
        AuthenticationData _authenticationData => _authenticationDataVariable.Value;
        
        private const bool IS_CONSENTED = true;
        private bool _isConnected = true;

        private void OnEnable()
        {
            _authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;
            _networkMonitorData.OnNetworkFound.OnRaised += OnNetworkEnabled;
            _networkMonitorData.OnNetworkLost.OnRaised += OnNetworkDisabled;
        }

        private void OnDisable()
        {
            _authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;
            _networkMonitorData.OnNetworkFound.OnRaised -= OnNetworkEnabled;
            _networkMonitorData.OnNetworkLost.OnRaised -= OnNetworkDisabled;
        }

        void OnAuthenticationSignedIn()
        {
            AskForConsents();
            SetUserId();
        }

        /// <summary>
        /// Ask For Consents - Async
        /// Ask User consents for data collection
        /// </summary>
        private void AskForConsents()
        {
            if(IS_CONSENTED && _isConnected) AnalyticsService.Instance.StartDataCollection();
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
            if (!_isConnected || !IS_CONSENTED) return;
            UnityServices.ExternalUserId = SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// Force Upload Data 
        /// Force upload data to Unity Analytics Services if network detected and is user consented
        /// </summary>
        public void ForceUploadData()
        {
            if (!_isConnected || !IS_CONSENTED) return;
            AnalyticsService.Instance.Flush();
        }
    }
}
