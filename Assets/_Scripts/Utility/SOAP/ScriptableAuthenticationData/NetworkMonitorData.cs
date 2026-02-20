using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [System.Serializable]
    public class NetworkMonitorData
    {
        [SerializeField]
        float refreshInterval;
        
        public ScriptableEventNoParam OnNetworkFound;
        public ScriptableEventNoParam OnNetworkLost;
    }
}