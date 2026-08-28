using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    public class NetworkMonitorData
    {
        [SerializeField]
        float refreshInterval;

        public ScriptableEventNoParam OnNetworkFound;
        public ScriptableEventNoParam OnNetworkLost;

        /// <summary>Mirror of NetworkMonitor's last observed state. Written by NetworkMonitor on every transition; read by NetworkDiagnostics.</summary>
        public bool IsOnline { get; internal set; }

        /// <summary><see cref="Time.unscaledTime"/> of the most recent Online ↔ Offline transition. Written by NetworkMonitor.</summary>
        public float LastTransitionUnscaledTime { get; internal set; }
    }
}