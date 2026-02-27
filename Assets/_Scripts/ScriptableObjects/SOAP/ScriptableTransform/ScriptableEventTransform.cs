using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Transform), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(Transform))]
    public class ScriptableEventTransform : ScriptableEvent<Transform>
    {
        
    }
}
