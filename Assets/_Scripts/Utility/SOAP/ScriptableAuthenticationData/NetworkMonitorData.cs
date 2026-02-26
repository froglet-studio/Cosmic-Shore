using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utility.SOAP
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