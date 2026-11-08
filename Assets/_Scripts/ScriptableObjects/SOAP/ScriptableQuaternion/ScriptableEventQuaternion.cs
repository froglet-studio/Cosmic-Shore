using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Quaternion), menuName = "ScriptableObjects/Events/"+ nameof(Quaternion))]
    public class ScriptableEventQuaternion : ScriptableEvent<Quaternion>
    {
        
    }
}
