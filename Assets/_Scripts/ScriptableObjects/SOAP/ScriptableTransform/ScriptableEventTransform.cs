using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Transform), menuName = "ScriptableObjects/Events/"+ nameof(Transform))]
    public class ScriptableEventTransform : ScriptableEvent<Transform>
    {
        
    }
}
