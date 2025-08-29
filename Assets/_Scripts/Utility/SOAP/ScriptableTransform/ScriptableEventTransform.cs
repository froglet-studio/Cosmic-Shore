using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(Transform), menuName = "Soap/ScriptableEvents/"+ nameof(Transform))]
    public class ScriptableEventTransform : ScriptableEvent<Transform>
    {
        
    }
}
