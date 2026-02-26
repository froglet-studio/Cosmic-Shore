using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Variable_" + nameof(NetworkMonitorData), menuName = "ScriptableObjects/SOAP/Variables/"+ nameof(NetworkMonitorData))]
    public class NetworkMonitorDataVariable : ScriptableVariable<NetworkMonitorData>
    {
            
    }
}