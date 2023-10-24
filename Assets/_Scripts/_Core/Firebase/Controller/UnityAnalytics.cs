using StarWriter.Utility.Singleton;
using Unity.Services.Core;
using UnityEngine;

namespace _Scripts._Core.Firebase.Controller
{
    public class UnityAnalytics : SingletonPersistent<UnityAnalytics>
    {
        
        private bool _isConnected = true;
        // Start is called before the first frame update
        void Start()
        {
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
            UnityServices.ExternalUserId = SystemInfo.deviceUniqueIdentifier;
        }
    }
}
