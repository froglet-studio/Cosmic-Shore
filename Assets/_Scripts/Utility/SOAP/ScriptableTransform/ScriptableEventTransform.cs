using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Transform), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(Transform))]
    public class ScriptableEventTransform : ScriptableEvent<Transform>
    {
        
    }
}
