using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Quaternion), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(Quaternion))]
    public class ScriptableEventQuaternion : ScriptableEvent<Quaternion>
    {
        
    }
}
