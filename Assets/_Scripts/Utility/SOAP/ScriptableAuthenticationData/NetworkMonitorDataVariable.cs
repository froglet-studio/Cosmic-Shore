using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_variable_" + nameof(NetworkMonitorData), menuName = "Soap/ScriptableVariables/"+ nameof(NetworkMonitorData))]
    public class NetworkMonitorDataVariable : ScriptableVariable<NetworkMonitorData>
    {
            
    }
}