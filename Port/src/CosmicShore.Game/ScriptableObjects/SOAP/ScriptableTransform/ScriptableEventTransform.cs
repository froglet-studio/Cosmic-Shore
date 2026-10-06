using CosmicShore.Engine;
using CosmicShore.Engine.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(Transform), menuName = "ScriptableObjects/Events/"+ nameof(Transform))]
    public class ScriptableEventTransform : ScriptableEvent<Transform>
    {
        
    }
}
