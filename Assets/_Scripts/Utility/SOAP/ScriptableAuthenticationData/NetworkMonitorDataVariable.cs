using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(NetworkMonitorData), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(NetworkMonitorData))]
    public class NetworkMonitorDataVariable : ScriptableVariable<NetworkMonitorData>
    {
            
    }
}